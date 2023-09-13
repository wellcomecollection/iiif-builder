using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Presentation;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Utils;
using Utils.Web;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Presentation;
using Wellcome.Dds.Repositories.Presentation.V2;
using Wellcome.Dds.Server.Conneg;
using Collection = IIIF.Presentation.V3.Collection;
using Manifest = IIIF.Presentation.V3.Manifest;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.Server.Controllers
{
    /// <summary>
    /// Mostly now just a Proxy to S3 resources made by WorkflowProcessor.
    /// </summary>
    [FeatureGate(FeatureFlags.PresentationServices)]
    [Route("[controller]")]
    [ApiController]
    public class PresentationController : ControllerBase
    {
        private readonly DdsOptions ddsOptions;
        private readonly Helpers helpers;
        private readonly UriPatterns uriPatterns;
        private readonly DdsContext ddsContext;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly ICatalogue catalogue;
        private readonly LinkRewriter linkRewriter;
        private readonly ILogger<PresentationController> logger;

        public PresentationController(
            ILogger<PresentationController> logger,
            IOptions<DdsOptions> options,
            Helpers helpers,
            UriPatterns uriPatterns,
            DdsContext ddsContext,
            IIIIFBuilder iiifBuilder,
            ICatalogue catalogue,
            LinkRewriter linkRewriter
            )
        {
            this.logger = logger;
            ddsOptions = options.Value;
            this.helpers = helpers;
            this.uriPatterns = uriPatterns;
            this.ddsContext = ddsContext;
            this.iiifBuilder = iiifBuilder;
            this.catalogue = catalogue;
            this.linkRewriter = linkRewriter;
        }

        /// <summary>
        /// The canonical route for IIIF resources.
        /// Supports content negotiation.
        ///
        /// This is simply a proxy to resources in S3.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{*id}")] 
        public async Task<IActionResult> Index(string id)
        {
            logger.LogDebug("IIIF Resource request for {id}", id);
            var ddsId = new DdsIdentifier(id);
            var redirect = RequiredRedirect(ddsId, id, ManifestTransformer);
            if (redirect != null)
            {
                logger.LogDebug("{id} required redirect to {redirect}", id, redirect);
                return Redirect(redirect);
            }
            // Return requested version if headers present, or fallback to known version
            var iiifVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType(Version.V3);
            if (iiifVersion == Version.V3)
            {
                if (! await helpers.ExistsInStorage(ddsOptions.PresentationContainer, $"v3/{ddsId}"))
                {
                    logger.LogInformation("{ddsId} does not exist in storage on v3 path", ddsId.ToString());
                    return await HandleMissingPresentationResource(ddsId);
                }
            }
            return iiifVersion == Version.V2 ? await V2(ddsId) : await V3(ddsId);
        }

        /// <summary>
        /// The JSON is not in storage
        /// </summary>
        /// <param name="ddsId"></param>
        /// <returns></returns>
        private async Task<IActionResult> HandleMissingPresentationResource(DdsIdentifier ddsId)
        {
            var id = ddsId.ToString();
            // The requested identifier does not exist in storage (e.g., in S3 bucket).
            // But we might have been asked for a different form of identifier, and we can redirect to the correct form.
            var alternative = ddsContext.GetManifestationByAnyIdentifier(id);
            if (alternative != null)
            {
                logger.LogInformation("Found alternative Manifestation for {id}: {altId}", id, alternative.ManifestationIdentifier);
                if (alternative.ManifestationIdentifier != id)
                {
                    // This was requested with an alternative identifier
                    logger.LogInformation("RedirectPermanent to {altId}", alternative.ManifestationIdentifier);
                    return RedirectPermanent(uriPatterns.Manifest(alternative.ManifestationIdentifier));
                }
                
                logger.LogInformation("Returning 404 as alternative has same id {id}", id);
                return NotFound();
            }
            
            // It's not a manifestation, but is it actually a collection?
            // There won't be JSON for that...
            return await ArchivesReference(id, true);
        }

        private string ManifestTransformer(string s)
        {
            var manifest = uriPatterns.Manifest(s);
            return linkRewriter.TransformIdentifier(manifest);
        }

        /// <summary>
        /// Non conneg explicit path for IIIF 2.1
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v2/{*id}")]
        public Task<IActionResult> V2(string id) => GetIIIFResource($"v2/{id}", IIIFPresentation.ContentTypes.V2);

        /// <summary>
        /// Non conneg explicit path for IIIF 3.0
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v3/{*id}")]
        public Task<IActionResult> V3(string id) => GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            logger.LogDebug("Serving IIIF Content for {path}", path);
            return await helpers.ServeIIIFContent(ddsOptions.PresentationContainer, path, contentType, this);
        }

        private IActionResult CollectionContent(IIIF.Presentation.V3.Collection coll)
        {
            logger.LogDebug("Returning V3 CollectionContent for {collectionId}", coll.Id);
            return CollectionContent(coll.AsJson(), IIIFPresentation.ContentTypes.V3);
        }
        
        private IActionResult CollectionContent(IIIF.Presentation.V2.Collection coll)
        {
            logger.LogDebug("Returning V2 CollectionContent for {collectionId}", coll.Id);
            return CollectionContent(coll.AsJson(), IIIFPresentation.ContentTypes.V2);
        }

        private IActionResult CollectionContent(string json, string contentType)
        {
            if (linkRewriter.RequiresRewriting())
            {
                return Content(linkRewriter.RewriteLinks(json), contentType);
            }
            return Content(json, contentType);
        }
        
        
        /// <summary>
        /// The root IIIF collection, IIIF 3.
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections")]
        [HttpGet("v3/collections")]
        public IActionResult TopLevelCollection()
        {
            logger.LogDebug("Request for V3 top level collection");
            var tlc = new Collection
            {
                Id = uriPatterns.CollectionForAggregation(),
                Label = new LanguageMap("en", "Wellcome Collection"),
                Items = new List<ICollectionItem>
                {
                    MakeAggregationCollection("subjects", "Works by subject"),
                    MakeAggregationCollection("genres", "Works by genre"),
                    MakeAggregationCollection("contributors", "Works by contributor"),
                    MakeAggregationCollection("digitalcollections", "Works by digital collection"),
                    MakeAggregationCollection("archives", "Archive collections")
                }
            };
            tlc.EnsurePresentation3Context();
            return CollectionContent(tlc);
        }
        
        /// <summary>
        /// The root IIIF collection, IIIF 2.
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections")]
        public IActionResult TopLevelCollectionV2()
        {
            logger.LogDebug("Request for V2 top level collection");
            var tlc = new IIIF.Presentation.V2.Collection
            {
                Id = uriPatterns.CollectionForAggregation().AsV2(),
                Label = new MetaDataValue("Wellcome Collection"),
                Collections = new List<IIIF.Presentation.V2.Collection>
                {
                    MakeAggregationCollectionV2("subjects", "Works by subject"),
                    MakeAggregationCollectionV2("genres", "Works by genre"),
                    MakeAggregationCollectionV2("contributors", "Works by contributor"),
                    MakeAggregationCollectionV2("digitalcollections", "Works by digital collection"),
                    MakeAggregationCollectionV2("archives", "Archive collections")
                }
            };
            tlc.EnsurePresentation2Context();
            return CollectionContent(tlc);
        }


        /// <summary>
        /// IIIF v3 top level collection for archives, replaces the "lightweight" v2 version
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/archives")]
        [HttpGet("v3/collections/archives")]
        public IActionResult ArchivesTopLevelCollection()
        {
            logger.LogDebug("Request for V3 top level ARCHIVES collection");
            var archiveRoot = CollectionForAggregationId("archives");
            var coll = new Collection
            {
                Id = archiveRoot,
                Label = new LanguageMap("en", "Archives at Wellcome Collection"),
                Items = new List<ICollectionItem>()
            };
            archiveRoot += "/";
            ArchiveCollectionTop noCollection = null;
            foreach (var topLevelArchiveCollection in ddsContext
                .GetTopLevelArchiveCollections()
                .OrderBy(tla => tla.Title))
            {
                if (topLevelArchiveCollection.ReferenceNumber == Manifestation.EmptyTopLevelArchiveReference)
                {
                    noCollection = topLevelArchiveCollection;
                }
                else
                {
                    coll.Items.Add(new Collection
                    {
                        Id = archiveRoot + topLevelArchiveCollection.ReferenceNumber,
                        Label = new LanguageMap("en", topLevelArchiveCollection.Title)
                    });
                }
            }

            if (noCollection != null)
            {
                coll.Items.Add(new Collection
                {
                    Id = archiveRoot + noCollection.ReferenceNumber,
                    Label = new LanguageMap("en", noCollection.Title)
                });
            }
            coll.EnsurePresentation3Context();
            return CollectionContent(coll);
        }

        /// <summary> 
        /// IIIF v2 top level collection for archives, replaces the "lightweight" v2 version
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/archives")]
        public IActionResult ArchivesTopLevelCollectionV2()
        {
            logger.LogDebug("Request for V2 top level ARCHIVES collection");
            var archiveRoot = CollectionForAggregationId("archives").AsV2();
            var coll = new IIIF.Presentation.V2.Collection
            {
                Id = archiveRoot,
                Label = new MetaDataValue("Archives at Wellcome Collection"),
                Members = new List<IIIFPresentationBase>()
            };
            archiveRoot += "/";
            ArchiveCollectionTop noCollection = null;
            foreach (var topLevelArchiveCollection in ddsContext
                .GetTopLevelArchiveCollections()
                .OrderBy(tla => tla.Title))
            {
                if (topLevelArchiveCollection.ReferenceNumber == Manifestation.EmptyTopLevelArchiveReference)
                {
                    noCollection = topLevelArchiveCollection;
                }
                else
                {
                    coll.Members.Add(new IIIF.Presentation.V2.Collection
                    {
                        Id = archiveRoot + topLevelArchiveCollection.ReferenceNumber,
                        Label = new MetaDataValue(topLevelArchiveCollection.Title)
                    });
                }
            }

            if (noCollection != null)
            {
                coll.Members.Add(new IIIF.Presentation.V2.Collection
                {
                    Id = archiveRoot + noCollection.ReferenceNumber,
                    Label = new MetaDataValue(noCollection.Title)
                });
            }
            coll.EnsurePresentation2Context();
            return CollectionContent(coll);
        }
        
        /// <summary>
        /// IIIF 3 - Handles all archive CALM ref numbers.
        /// If this is actually a manifest it will redirect to the canonical DDS URL
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/archives/{*referenceNumber}")]
        [HttpGet("v3/collections/archives/{*referenceNumber}")]
        public async Task<IActionResult> ArchivesReference(string referenceNumber, bool fromPresentationPath = false)
        {
            logger.LogDebug("Handling potential V3 Archives reference {referenceNumber} on {requestPath}", 
                referenceNumber, Request.Path);
            Collection collection;
            if (referenceNumber == Manifestation.EmptyTopLevelArchiveReference)
            {
                logger.LogDebug("{referenceNumber} is top level collection", referenceNumber);
                collection = GetNoCollectionArchive();
            }
            else
            {
                var work = await catalogue.GetWorkByOtherIdentifier(referenceNumber);
                if (work == null)
                {
                    return NotFound();
                }
                if (work.HasIIIFDigitalLocation())
                {
                    logger.LogDebug("{referenceNumber} has digital location", referenceNumber);
                    var refAsDdsId = new DdsIdentifier(referenceNumber);
                    // Should we instead get the work from the Manifestations table at this point?
                    var bNumber = work.GetSierraSystemBNumbers().FirstOrDefault();
                    if (bNumber.HasText())
                    {
                        logger.LogDebug("Found bNumber for {referenceNumber}: {bNumber}", referenceNumber, bNumber);
                        var bNumberAsDdsId = new DdsIdentifier(bNumber);
                        // There's a slim chance of a circular redirect without checking this;
                        // don't redirect if it's already a b-number. It shouldn't be! 
                        if (refAsDdsId != bNumberAsDdsId)
                        {
                            logger.LogDebug("They are different, so redirecting to {bNumber}", bNumber);
                            return Redirect(uriPatterns.Manifest(bNumber));
                        }
                        logger.LogDebug("{refAsDdsId} and {bNumberAsDdsId} are the same so return 404", refAsDdsId, bNumberAsDdsId);
                        return NotFound();
                    }
                    
                    // TODO - need to decide whether to use the manifestations data or the digital location from the work
                    // we already have the digital location, what does it look like for born digital?
                    // or we can do this:
                    // ddsContext.GetManifestationByAnyIdentifier(referenceNumber);
                    // ... redirect... avoiding circular ref.
                    
                    logger.LogInformation("Has digital location but can't get bNumber for {referenceNumber}, return 404", referenceNumber);
                    // but for now:
                    return NotFound();
                }

                if (fromPresentationPath)
                {
                    var pattern = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber);
                    logger.LogDebug("The user has likely hacked back the URL path to get here, so redirect to {pattern}", pattern);
                    return Redirect(pattern);
                }
                logger.LogDebug("Build an archival node (hierarchical container) for {workId}", work.Id);
                collection = iiifBuilder.BuildArchiveNode(work);
                
            }

            if (collection == null)
            {
                logger.LogDebug("Could not build archival node so return 404");
                return NotFound();
            }
            
            collection.EnsurePresentation3Context();
            return CollectionContent(collection);
        }
        
        
        /// <summary> 
        /// IIIF 2 - Handles all archive CALM ref numbers.
        /// If this is actually a manifest it will redirect to the canonical DDS URL
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/archives/{*referenceNumber}")]
        public async Task<IActionResult> ArchivesReferenceV2(string referenceNumber)
        {
            logger.LogDebug("Handling potential V2 Archives reference {referenceNumber} on {requestPath}", 
                referenceNumber, Request.Path);
            IIIF.Presentation.V2.Collection collection;
            if (referenceNumber == Manifestation.EmptyTopLevelArchiveReference)
            {
                collection = GetNoCollectionArchiveV2();
            }
            else
            {
                var work = await catalogue.GetWorkByOtherIdentifier(referenceNumber);
                if (work == null)
                {
                    return NotFound();
                }
                if (work.HasIIIFDigitalLocation())
                {
                    var bNumber = work.GetSierraSystemBNumbers().First();
                    return Redirect(uriPatterns.Manifest(bNumber).AsV2());
                }

                var v3Coll = iiifBuilder.BuildArchiveNode(work);
                if (v3Coll == null)
                {
                    return NotFound();
                }
                collection = ConverterHelpers.GetIIIFPresentationBase<IIIF.Presentation.V2.Collection>(v3Coll);
                collection.Id = v3Coll.Id.AsV2();
                if (v3Coll.Items.HasItems())
                {
                    collection.Members = v3Coll.Items.Select(ConvertArchiveCollectionMemberToV2).ToList();
                }
            }
            collection.EnsurePresentation2Context();
            return CollectionContent(collection);
        }

        private IIIFPresentationBase ConvertArchiveCollectionMemberToV2(ICollectionItem item)
        {
            switch (item)
            {
                case Collection collection:
                    return new IIIF.Presentation.V2.Collection
                    {
                        Id = collection.Id.AsV2(),
                        Label = new MetaDataValue(collection.Label.ToString())
                    };
                case Manifest manifest:
                    return new IIIF.Presentation.V2.Manifest
                    {
                        Id = manifest.Id.AsV2(),
                        Label = new MetaDataValue(manifest.Label.ToString())
                    };
            }

            return null;
        }

        private Collection GetNoCollectionArchive()
        {
            logger.LogDebug("GetNoCollectionArchive V3");
            var collection = new Collection
            {
                Id = uriPatterns.CollectionForAggregation("archives", Manifestation.EmptyTopLevelArchiveReference),
                Label = new LanguageMap("en", Manifestation.EmptyTopLevelArchiveTitle),
                Items = new List<ICollectionItem>()
            };
            foreach (var manifestation in ddsContext.Manifestations.Where(m => m.CollectionTitle == "(no-title)"))
            {
                var labels = new[] {manifestation.PackageLabel, manifestation.ReferenceNumber};
                collection.Items.Add(new Manifest
                {
                    
                    Id = uriPatterns.Manifest(manifestation.PackageIdentifier),
                    Label = new LanguageMap("en", labels),
                    Thumbnail = manifestation.GetThumbnail()
                });
            }

            return collection;
        }
        
        
        private IIIF.Presentation.V2.Collection GetNoCollectionArchiveV2()
        {
            logger.LogDebug("GetNoCollectionArchive V2");
            var collection = new IIIF.Presentation.V2.Collection
            {
                Id = uriPatterns.CollectionForAggregation("archives", Manifestation.EmptyTopLevelArchiveReference).AsV2(),
                Label = new MetaDataValue(Manifestation.EmptyTopLevelArchiveTitle),
                Members = new List<IIIFPresentationBase>()
            };
            foreach (var manifestation in ddsContext.Manifestations.Where(m => m.CollectionTitle == "(no-title)"))
            {
                collection.Members.Add(new IIIF.Presentation.V2.Manifest
                {
                    Id = uriPatterns.Manifest(manifestation.PackageIdentifier).AsV2(),
                    Label = new MetaDataValue(manifestation.ReferenceNumber + " - " + manifestation.PackageLabel)
                });
            }

            return collection;
        }
        
        /// <summary>
        /// An aggregation - subjects, contributors etc
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}")]
        [HttpGet("v3/collections/{aggregator}")]
        public IActionResult Aggregation(string aggregator)
        {
            logger.LogDebug("Building V3 aggregation for {aggregator}", aggregator);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new Collection
            {
                Id = CollectionForAggregationId(aggregator),
                Label = new LanguageMap("en", $"Works by {apiType}, by initial"),
                Items = new List<ICollectionItem>()
            };
            var chunkInitials = ddsContext.GetChunkInitials(apiType);
            foreach (char initial in chunkInitials)
            {
                coll.Items.Add(new Collection
                {
                    Id = CollectionForAggregationId(aggregator, null, initial),
                    Label = new LanguageMap("en", $"{apiType}s starting with {initial}")
                });
            }
            coll.EnsurePresentation3Context();
            return CollectionContent(coll);
        }
        
        
        /// <summary>
        /// A partitioned aggregation - subjects, contributors etc
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/chunked/{aggregator}/{chunk}")]
        [HttpGet("v3/collections/chunked/{aggregator}/{chunk}")]        
        public IActionResult ChunkedAggregation(string aggregator, char chunk)
        {
            logger.LogDebug("Building CHUNKED V3 aggregation for {aggregator}", aggregator);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new Collection
            {
                Id = CollectionForAggregationId(aggregator),
                Label = new LanguageMap("en", "Works by " + apiType),
                Items = new List<ICollectionItem>()
            };
            var aggregation = ddsContext.GetChunkedAggregation(apiType, chunk);
            foreach (var aggregationMetadata in aggregation)
            {
                if (aggregationMetadata.Identifier != "Electronic_books.")
                {
                    coll.Items.Add(new Collection
                    {
                        Id = CollectionForAggregationId(aggregator, aggregationMetadata.Identifier),
                        Label = new LanguageMap("none", aggregationMetadata.Label)
                    });
                }
            }
            coll.EnsurePresentation3Context();
            return CollectionContent(coll);
        }
        
        
        
        
        /// <summary>
        /// An aggregation - subjects, contributors etc
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/{aggregator}")]
        public IActionResult AggregationV2(string aggregator)
        {
            logger.LogDebug("Building V2 aggregation for {aggregator}", aggregator);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new IIIF.Presentation.V2.Collection
            {
                Id = CollectionForAggregationId(aggregator).AsV2(),
                Label = new MetaDataValue($"Works by {apiType}, by initial"),
                Members = new List<IIIFPresentationBase>()
            };
            
            var chunkInitials = ddsContext.GetChunkInitials(apiType);
            foreach (char initial in chunkInitials)
            {
                coll.Members.Add(new IIIF.Presentation.V2.Collection
                {
                    Id = CollectionForAggregationId(aggregator, null, initial).AsV2(),
                    Label = new MetaDataValue($"{apiType}s starting with {initial}")
                });
            }
            coll.EnsurePresentation2Context();
            return CollectionContent(coll);
        }
        
        /// <summary>
        /// A partitioned aggregation - subjects, contributors etc
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/chunked/{aggregator}/{chunk}")]        
        public IActionResult ChunkedAggregationV2(string aggregator, char chunk)
        {
            logger.LogDebug("Building CHUNKED V2 aggregation for {aggregator}", aggregator);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new IIIF.Presentation.V2.Collection
            {
                Id = CollectionForAggregationId(aggregator).AsV2(),
                Label = new MetaDataValue("Works by " + apiType),
                Members = new List<IIIFPresentationBase>()
            };
            var aggregation = ddsContext.GetChunkedAggregation(apiType, chunk);
            foreach (var aggregationMetadata in aggregation)
            {
                if (aggregationMetadata.Identifier != "Electronic_books.")
                {
                    coll.Members.Add(new IIIF.Presentation.V2.Collection
                    {
                        Id = CollectionForAggregationId(aggregator, aggregationMetadata.Identifier).AsV2(),
                        Label = new MetaDataValue(aggregationMetadata.Label)
                    });
                }
            }
            coll.EnsurePresentation2Context();
            return CollectionContent(coll);
        }

        

        /// <summary>
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}/{value}")]
        [HttpGet("v3/collections/{aggregator}/{value}")]
        public IActionResult ManifestsByAggregationValue(string aggregator, string value)
        {
            logger.LogDebug("Building V3 ManifestsByAggregationValue for {aggregator}/{value}", aggregator, value);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            int total = ddsContext.GetAggregationCount(apiType, value);
            if (total > ddsOptions.IIIFCollectionAggregationMaxManifests)
            {
                return MakeManifestsByAggregationValueCollection(total, aggregator, value);
            }
            const int maxThumbnails = 50; // to avoid huge collections
            var coll = new Collection
            {
                Id = CollectionForAggregationId(aggregator, value),
                Items = new List<ICollectionItem>()
            };
            int thumbnailCount = 0;
            foreach (var result in ddsContext.GetAggregation(apiType, value))
            {
                var manifest = new Manifest
                {
                    Id = uriPatterns.Manifest(result.Manifestation.PackageIdentifier),
                    Label = new LanguageMap("none", result.Manifestation.PackageLabel)
                };
                coll.Items.Add(manifest);
                coll.Label ??= new LanguageMap("none", $"{result.CollectionLabel}: {result.CollectionStringValue}");
                if (thumbnailCount++ < maxThumbnails)
                {
                    manifest.Thumbnail = result.Manifestation.GetThumbnail();
                }
            }
            coll.EnsurePresentation3Context();
            Response.CacheForDays(30);
            return CollectionContent(coll);
        }

        private IActionResult MakeManifestsByAggregationValueCollection(int total, string aggregator, string value)
        {
            var coll = new Collection
            {
                Id = CollectionForAggregationId(aggregator, value),
                Items = new List<ICollectionItem>()
            };
            int pages = total / ddsOptions.IIIFCollectionAggregationMaxManifests;
            if (total % ddsOptions.IIIFCollectionAggregationMaxManifests > 0)
            {
                pages += 1;
            }
            int page = 1;
            while (page <= pages)
            {
                var start = (page - 1) * ddsOptions.IIIFCollectionAggregationMaxManifests + 1;
                var end = start + 99;  // Deliberately do not make this the exact number - this makes it more persistent
                var pageColl = new Collection
                {
                    Id = $"{coll.Id}/paged/{start}-{end}",
                    Label = new LanguageMap("en", $"Page {page} of {aggregator}/{value}")
                };
                coll.Items.Add(pageColl);
                page++;
            }
            
            coll.EnsurePresentation3Context();
            Response.CacheForDays(30);
            return CollectionContent(coll);
        }


        /// <summary>
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}/{value}/paged/{range}")]
        [HttpGet("v3/collections/{aggregator}/{value}/paged/{range}")]
        public IActionResult ManifestsByAggregationValuePage(string aggregator, string value, string range)
        {
            int skip = 0;
            int take = ddsOptions.IIIFCollectionAggregationMaxManifests;

            if (range.HasText() && range.IndexOf("-", StringComparison.Ordinal) != -1)
            {
                int rangeStart;
                if (int.TryParse(range.SplitByDelimiterIntoArray('-')[0], out rangeStart))
                {
                    skip = rangeStart - 1;
                }
            }
            
            var coll = new Collection
            {
                Id = $"{CollectionForAggregationId(aggregator, value)}/paged/{range}",
                Items = new List<ICollectionItem>()
            };
            int thumbnailCount = 0;
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            foreach (var result in ddsContext.GetAggregation(apiType, value, skip, take))
            {
                var manifest = new Manifest
                {
                    Id = uriPatterns.Manifest(result.Manifestation.PackageIdentifier),
                    Label = new LanguageMap("none", result.Manifestation.PackageLabel)
                };
                coll.Items.Add(manifest);
                coll.Label ??= new LanguageMap("none", $"{range} - {result.CollectionLabel}: {result.CollectionStringValue}");
            }
            coll.EnsurePresentation3Context();
            Response.CacheForDays(30);
            return CollectionContent(coll);
        }


        /// <summary>
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/{aggregator}/{value}")]
        public IActionResult ManifestsByAggregationValueV2(string aggregator, string value)
        {
            logger.LogDebug("Building V2 ManifestsByAggregationValue for {aggregator}/{value}", aggregator, value);
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new IIIF.Presentation.V2.Collection
            {
                Id = CollectionForAggregationId(aggregator, value).AsV2(),
                Members = new List<IIIFPresentationBase>()
            };
            foreach (var result in ddsContext.GetAggregation(apiType, value))
            {
                var manifest = new IIIF.Presentation.V2.Manifest
                {
                    Id = uriPatterns.Manifest(result.Manifestation.PackageIdentifier).AsV2(),
                    Label = new MetaDataValue(result.Manifestation.PackageLabel)
                };
                coll.Members.Add(manifest);
                coll.Label ??= new MetaDataValue($"{result.CollectionLabel}: {result.CollectionStringValue}");
            }
            coll.EnsurePresentation2Context();
            Response.CacheForDays(30);
            return CollectionContent(coll);
        }
        
        
        private Collection MakeAggregationCollection(string aggregator, string label)
        {
            return new()
            {
                Id = CollectionForAggregationId(aggregator),
                Label = new LanguageMap("en", label)
            };
        }
        
        
        private IIIF.Presentation.V2.Collection MakeAggregationCollectionV2(string aggregator, string label)
        {
            return new()
            {
                Id = CollectionForAggregationId(aggregator).AsV2(),
                Label = new MetaDataValue(label)
            };
        }

        /// <summary>
        /// Allow ID domain to be rewritten for local dev convenience
        /// </summary>
        /// <param name="aggregator"></param>
        /// <param name="value"></param>
        /// <param name="chunk"></param>
        /// <returns></returns>
        private string CollectionForAggregationId(string aggregator, string value = null, char chunk = Char.MinValue)
        {
            string id;
            if (chunk != char.MinValue)
            {
                id = uriPatterns.CollectionForAggregation(aggregator, chunk);
            }
            else if (value != null)
            {
                id = uriPatterns.CollectionForAggregation(aggregator, value);
            }
            else
            {
                id = uriPatterns.CollectionForAggregation(aggregator);
            }

            if (string.IsNullOrWhiteSpace(ddsOptions.RewriteDomainLinksTo))
            {
                return id;
            }
            return id.Replace(ddsOptions.LinkedDataDomain!, ddsOptions.RewriteDomainLinksTo);

        }
        
        private string RequiredRedirect(DdsIdentifier ddsId, string requestedForm, Func<string, string> transformer)
        {
            if (ddsId.HasBNumber)
            {
                // Don't call NormaliseBNumber without some lightweight new-DDS-specific checks first
                if (requestedForm.Contains('_'))
                {
                    // Likely a manifestation identifier, proceed
                    return null;
                }
                if (requestedForm.StartsWith('b') && requestedForm.Length == 9)
                {
                    // looks like a normal b-number
                    char checkDigit = requestedForm[8];
                    if (Char.IsDigit(checkDigit) || 'x' == checkDigit)
                    {
                        // still looks like a normal b number. In the new DDS, where we don't expect 
                        // Sierra links directly, we WON'T normalise these. An incorrect check digit is a 404,
                        // rather than something we correct. However, if the check digit is `a` we will correct it.
                        return null;
                    }
                }
                var normalised = WellcomeLibraryIdentifiers.GetNormalisedBNumber(requestedForm, false);
                if (normalised != requestedForm)
                {
                    return transformer(normalised);
                }
            }

            if (ddsId.ToString() != requestedForm)
            {
                // We want the normalised form of the identifier
                return transformer(ddsId.ToString());
            }
            
            return null;
        }
    }
    
    

    static class PresentationControllerX
    {
        public static string AsV2(this string id)
        {
            return id.Replace("/presentation/", "/presentation/v2/");
        }
    }
}