using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public PresentationController(
            IOptions<DdsOptions> options,
            Helpers helpers,
            UriPatterns uriPatterns,
            DdsContext ddsContext,
            IIIIFBuilder iiifBuilder,
            ICatalogue catalogue
            )
        {
            ddsOptions = options.Value;
            this.helpers = helpers;
            this.uriPatterns = uriPatterns;
            this.ddsContext = ddsContext;
            this.iiifBuilder = iiifBuilder;
            this.catalogue = catalogue;
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
        public Task<IActionResult> Index(string id)
        {
            var ddsId = new DdsIdentifier(id);
            var redirect = RequiredRedirect(ddsId, id, ManifestTransformer);
            if (redirect != null)
            {
                return Task.FromResult<IActionResult>(redirect);
            }
            // Return requested version if headers present, or fallback to known version
            var iiifVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType(Version.V3);
            return iiifVersion == Version.V2 ? V2(ddsId) : V3(ddsId);
        }

        private string ManifestTransformer(string s)
        {
            var manifest = uriPatterns.Manifest(s);
            if (ddsOptions.RewriteDomainLinksTo.HasText())
            {
                manifest = manifest.Replace(ddsOptions.LinkedDataDomain, ddsOptions.RewriteDomainLinksTo);
            }

            return manifest;
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
            return await helpers.ServeIIIFContent(ddsOptions.PresentationContainer, path, contentType, this);
        }

        /// <summary>
        /// The root IIIF collection, IIIF 3.
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections")]
        [HttpGet("v3/collections")]
        public IActionResult TopLevelCollection()
        {
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
            return Content(tlc.AsJson(), IIIFPresentation.ContentTypes.V3);
        }
        
        /// <summary>
        /// The root IIIF collection, IIIF 2.
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections")]
        public IActionResult TopLevelCollectionV2()
        {
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
            return Content(tlc.AsJson(), IIIFPresentation.ContentTypes.V2);
        }


        /// <summary>
        /// IIIF v3 top level collection for archives, replaces the "lightweight" v2 version
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/archives")]
        [HttpGet("v3/collections/archives")]
        public IActionResult ArchivesTopLevelCollection()
        {
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
            
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V3);
        }

        /// <summary> 
        /// IIIF v2 top level collection for archives, replaces the "lightweight" v2 version
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/archives")]
        public IActionResult ArchivesTopLevelCollectionV2()
        {
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
            
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V2);
        }
        
        /// <summary>
        /// IIIF 3 - Handles all archive CALM ref numbers.
        /// If this is actually a manifest it will redirect to the canonical DDS URL
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/archives/{*referenceNumber}")]
        [HttpGet("v3/collections/archives/{*referenceNumber}")]
        public async Task<IActionResult> ArchivesReference(string referenceNumber)
        {
            Collection collection;
            if (referenceNumber == Manifestation.EmptyTopLevelArchiveReference)
            {
                collection = GetNoCollectionArchive();
            }
            else
            {
                var work = await catalogue.GetWorkByOtherIdentifier(referenceNumber);
                if (work.HasIIIFDigitalLocation())
                {
                    var bNumber = work.GetSierraSystemBNumbers().First();
                    return Redirect(uriPatterns.Manifest(bNumber));
                }

                collection = iiifBuilder.BuildArchiveNode(work);
                
            }
            return Content(collection.AsJson(), IIIFPresentation.ContentTypes.V3);
        }
        
        
        /// <summary> 
        /// IIIF 2 - Handles all archive CALM ref numbers.
        /// If this is actually a manifest it will redirect to the canonical DDS URL
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/archives/{*referenceNumber}")]
        public async Task<IActionResult> ArchivesReferenceV2(string referenceNumber)
        {
            IIIF.Presentation.V2.Collection collection;
            if (referenceNumber == Manifestation.EmptyTopLevelArchiveReference)
            {
                collection = GetNoCollectionArchiveV2();
            }
            else
            {
                var work = await catalogue.GetWorkByOtherIdentifier(referenceNumber);
                if (work.HasIIIFDigitalLocation())
                {
                    var bNumber = work.GetSierraSystemBNumbers().First();
                    return Redirect(uriPatterns.Manifest(bNumber).AsV2());
                }

                var v3Coll = iiifBuilder.BuildArchiveNode(work);
                collection = ConverterHelpers.GetIIIFPresentationBase<IIIF.Presentation.V2.Collection>(v3Coll);
                collection.Id = v3Coll.Id.AsV2();
                if (v3Coll.Items.HasItems())
                {
                    collection.Members = v3Coll.Items.Select(ConvertArchiveCollectionMemberToV2).ToList();
                }
            }
            return Content(collection.AsJson(), IIIFPresentation.ContentTypes.V2);
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
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new Collection
            {
                Id = CollectionForAggregationId(aggregator),
                Label = new LanguageMap("en", "Works by " + apiType),
                Items = new List<ICollectionItem>()
            };
            var aggregation = ddsContext.GetAggregation(apiType);
            foreach (var aggregationMetadata in aggregation)
            {
                coll.Items.Add(new Collection
                {
                    Id = CollectionForAggregationId(aggregator, aggregationMetadata.Identifier),
                    Label = new LanguageMap("none", aggregationMetadata.Label)
                });
            }
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V3);
        }
        
        
        /// <summary>
        /// An aggregation - subjects, contributors etc
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/{aggregator}")]
        public IActionResult AggregationV2(string aggregator)
        {
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
            var coll = new IIIF.Presentation.V2.Collection
            {
                Id = CollectionForAggregationId(aggregator).AsV2(),
                Label = new MetaDataValue("Works by " + apiType),
                Members = new List<IIIFPresentationBase>()
            };
            var aggregation = ddsContext.GetAggregation(apiType);
            foreach (var aggregationMetadata in aggregation)
            {
                coll.Members.Add(new IIIF.Presentation.V2.Collection
                {
                    Id = CollectionForAggregationId(aggregator, aggregationMetadata.Identifier).AsV2(),
                    Label = new MetaDataValue(aggregationMetadata.Label)
                });
            }
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V2);
        }

        

        /// <summary>
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}/{value}")]
        [HttpGet("v3/collections/{aggregator}/{value}")]
        public IActionResult ManifestsByAggregationValue(string aggregator, string value)
        {
            const int maxThumbnails = 50; // to avoid huge collections
            var apiType = Metadata.FromUrlFriendlyAggregator(aggregator);
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
            Response.CacheForDays(30);
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V3);
        }
        
                

        /// <summary>
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("v2/collections/{aggregator}/{value}")]
        public IActionResult ManifestsByAggregationValueV2(string aggregator, string value)
        {
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
            Response.CacheForDays(30);
            return Content(coll.AsJson(), IIIFPresentation.ContentTypes.V2);
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
        /// <param name="version"></param>
        /// <param name="aggregator"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private string CollectionForAggregationId(string aggregator, string value = null)
        {
            string id;
            if (value != null)
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
            return id.Replace(ddsOptions.LinkedDataDomain, ddsOptions.RewriteDomainLinksTo);

        }
        
        private RedirectResult RequiredRedirect(DdsIdentifier ddsId, string requestedForm, Func<string, string> transformer)
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
                    return RedirectPermanent(transformer(normalised));
                }
            }

            if (ddsId.ToString() != requestedForm)
            {
                // We want the normalised form of the identifier
                return RedirectPermanent(transformer(ddsId.ToString()));
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