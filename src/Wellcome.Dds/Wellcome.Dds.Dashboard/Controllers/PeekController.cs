using System;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class PeekController : Controller
    {
        private readonly IDds dds;
        private readonly ICatalogue catalogue;
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly ILogger<PeekController> logger;

        public PeekController(
            IDds dds,
            ICatalogue catalogue,
            IWorkStorageFactory workStorageFactory,
            ILogger<PeekController> logger,
            IIIIFBuilder iiifBuilder)
        {
            this.dds = dds;
            this.catalogue = catalogue;
            this.workStorageFactory = workStorageFactory;
            this.logger = logger;
            this.iiifBuilder = iiifBuilder;
        }

        public async Task<ContentResult> IIIFRaw(string id, bool all = false)
        {
            var ddsId = new DdsIdentifier(id);
            var build = await BuildResult(ddsId, all);
            return Content(build.IIIFResource.AsJson(), "application/json");
        }
        
        public async Task<ContentResult> IIIF2Raw(string id, bool all = false)
        {
            var ddsId = new DdsIdentifier(id);
            var build = await BuildResult(ddsId, all);

            var iiif2 = iiifBuilder.BuildLegacyManifestations(id, new[] {build});
            return Content(iiif2[id]?.IIIFResource.AsJson(), "application/json");
        }

        public async Task<ActionResult> IIIF(string id, bool all = false)
        {
            var ddsId = new DdsIdentifier(id);
            var build = await BuildResult(ddsId, all);
            var model = new CodeModel
            {
                Title = "IIIF Resource Preview",
                Description = "This has been built on the fly - it won't have been written to S3 yet.",
                BNumber = ddsId.BNumber,
                RelativePath = ddsId,
                Manifestation = ddsId,
                CodeAsString = build.IIIFResource.AsJson(),
                ErrorMessage = build.Message,
                Mode = "ace/mode/json",
                Raw = Url.Action("IIIFRaw", new {id}),
                IncludeLinksToFullBuild = true
            };
            return View("Code", model);
        }

        public async Task<ContentResult> XmlRaw(string id, string parts)
        {
            var store = await workStorageFactory.GetWorkStore(id);
            var xmlSource = await store.LoadXmlForPath(parts);
            return Content(xmlSource.XElement.ToString(), "text/xml");
        }
        
        public async Task<ActionResult> XmlView(string id, string parts)
        {
            var store = await workStorageFactory.GetWorkStore(id);
            string errorMessage = null;
            string xmlAsString = "";
            try
            {
                var xmlSource = await store.LoadXmlForPath(parts);
                xmlAsString = xmlSource.XElement.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading XML for id '{id}' with parts '{parts}'", id, parts);
                errorMessage = ex.Message;
            }

            var manifestation = id;
            try
            {
                var firstPart = parts.Split('/')[0].Split('.')[0];
                manifestation = firstPart;
            }
            catch 
            {
            }

            var model = new CodeModel
            {
                Title = "XML File View",
                Description = $"You can view other XML resources for {id} by changing the URL of this page.",
                BNumber = id,
                Manifestation = manifestation,
                RelativePath = parts,
                CodeAsString = xmlAsString,
                ErrorMessage = errorMessage,
                Mode = "ace/mode/xml",
                Raw = Url.Action("XmlRaw", new {id, parts})
            };
            
            string anchorFile = id.ToLowerInvariant() + ".xml";
            if (parts != anchorFile)
            {
                model.AnchorFile = anchorFile;
            }
            return View("Code", model);
        }
        
        public async Task<ContentResult> StorageManifestRaw(string id)
        {
            var archiveStore = (ArchiveStorageServiceWorkStore) await workStorageFactory.GetWorkStore(id);
            var storageManifest = await archiveStore.GetStorageManifest();
            return Content(storageManifest.ToString(Formatting.Indented), "application/json");
        }
        
        public async Task<ActionResult> StorageManifest(string id)
        {
            var archiveStore = (ArchiveStorageServiceWorkStore) await workStorageFactory.GetWorkStore(id);

            string errorMessage = null;
            string jsonAsString = null;
            try
            {
                var storageManifest = await archiveStore.GetStorageManifest();
                jsonAsString = storageManifest.ToString(Formatting.Indented);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting StorageManifest id '{id}'", id);
                errorMessage = ex.Message;
            }
            
            var model = new CodeModel
            {
                Title = "Storage Manifest",
                BNumber = id,
                Manifestation = id,
                CodeAsString = jsonAsString ?? string.Empty,
                ErrorMessage = errorMessage,
                Mode = "ace/mode/json",
                Raw = Url.Action("StorageManifestRaw", new {id})
            };
            return View("Code", model);
        }
        
        private async Task<BuildResult> BuildResult(DdsIdentifier ddsId, bool all)
        {
            var results = await BuildIIIF(ddsId, all);
            var build = results[ddsId];
            if (build.RequiresMultipleBuild && all == false)
            {
                results = await BuildIIIF(ddsId.BNumber, true);
                build = results[ddsId];
                // do we still have the same resource in the results?
                // This particular manifestation might have been removed.
                if (build == null)
                {
                    // e.g., AV MM rearranged into one Manifest
                    // So return the b number
                    build = results[ddsId.BNumber];
                }
            }
            return build;
        }
        
        private async Task<MultipleBuildResult> BuildIIIF(string id, bool all)
        {
            var ddsId = new DdsIdentifier(id);
            var work = await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
            await dds.RefreshManifestations(ddsId.BNumber, work);
            if (all)
            {
                return await iiifBuilder.BuildAllManifestations(id);
            }
            return await iiifBuilder.Build(id, work);
        }
    }
}