using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class PeekController : Controller
    {
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly ILogger<PeekController> logger;

        public PeekController(
            IWorkStorageFactory workStorageFactory,
            ILogger<PeekController> logger,
            IIIIFBuilder iiifBuilder)
        {
            this.workStorageFactory = workStorageFactory;
            this.logger = logger;
            this.iiifBuilder = iiifBuilder;
        }

        
        public async Task<ContentResult> IIIFRaw(string id)
        {
            var result = await iiifBuilder.Build(id);
            return Content(iiifBuilder.Serialise(result.IIIF3Resource), "application/json");
        }
        
        public async Task<ActionResult> IIIF(string id)
        {
            var ddsId = new DdsIdentifier(id);
            var result = await iiifBuilder.Build(ddsId);
            var model = new CodeModel
            {
                Title = "IIIF Resource Preview",
                Description = "This has been built live - it won't have been written to S3",
                BNumber = ddsId.BNumber,
                RelativePath = ddsId,
                Manifestation = ddsId,
                CodeAsString = iiifBuilder.Serialise(result.IIIF3Resource),
                ErrorMessage = result.Message,
                Mode = "ace/mode/json",
                Raw = Url.Action("IIIFRaw", new {id})
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
    }
}