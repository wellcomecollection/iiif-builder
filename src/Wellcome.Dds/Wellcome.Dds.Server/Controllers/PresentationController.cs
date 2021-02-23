using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Web;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Presentation;
using Wellcome.Dds.Server.Conneg;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.Server.Controllers
{
    /// <summary>
    /// Mostly now just a Proxy to S3 resources made by WorkflowProcessor.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class PresentationController : ControllerBase
    {
        private readonly DdsOptions ddsOptions;
        private readonly Helpers helpers;
        private UriPatterns uriPatterns;
        private DdsContext ddsContext;
        private IIIIFBuilder iiifBuilder;
        private ICatalogue catalogue;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="helpers"></param>
        /// <param name="uriPatterns"></param>
        /// <param name="ddsContext"></param>
        /// <param name="iiifBuilder"></param>
        /// <param name="catalogue"></param>
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
        [HttpGet("{id}")] 
        public Task<IActionResult> Index(string id)
        {
            // Return requested version if headers present, or fallback to known version
            var iiifVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType(Version.V3);
            return iiifVersion == Version.V2 ? V2(id) : V3(id);
        }

        /// <summary>
        /// Non conneg explicit path for IIIF 2.1
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v2/{id}")]
        public Task<IActionResult> V2(string id) => GetIIIFResource($"v2/{id}", IIIFPresentation.ContentTypes.V2);

        /// <summary>
        /// Non conneg explicit path for IIIF 3.0
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v3/{id}")]
        public Task<IActionResult> V3(string id) => GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            return await helpers.ServeIIIFContent(ddsOptions.PresentationContainer, path, contentType, this);
        }

        /// <summary>
        /// The root IIIF collection.
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections")]
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
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        [HttpGet("collections/archives")]
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
            foreach (var topLevelArchiveCollection in ddsContext.GetTopLevelArchiveCollections())
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
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/archives/{*referenceNumber}")]
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
                if (work.HasDigitalLocation())
                {
                    var bNumber = work.GetSierraSystemBNumbers().First();
                    return Redirect(uriPatterns.Manifest(bNumber));
                }

                collection = iiifBuilder.BuildArchiveNode(work);
                
            }
            return Content(collection.AsJson(), IIIFPresentation.ContentTypes.V3);
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
                var labels = new string[] {manifestation.Label, manifestation.ReferenceNumber};
                collection.Items.Add(new Manifest
                {
                    
                    Id = uriPatterns.Manifest(manifestation.PackageIdentifier),
                    Label = new LanguageMap("en", labels),
                    Thumbnail = manifestation.GetThumbnail()
                });
            }

            return collection;
        }


        /// <summary>
        /// An aggregation
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}")]
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
        /// A Collection of Manifests with the given metadata
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections/{aggregator}/{value}")]
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
                    Label = new LanguageMap("none", result.Manifestation.Label)
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
        
        
        private Collection MakeAggregationCollection(string aggregator, string label)
        {
            return new()
            {
                Id = CollectionForAggregationId(aggregator),
                Label = new LanguageMap("en", label)
            };
        }

        /// <summary>
        /// Allow ID domain to be rewritten for local dev convenience
        /// </summary>
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
    }
}