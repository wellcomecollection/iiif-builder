using System;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Presentation;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Utils;
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
        private readonly LinkRewriter linkRewriter;
        private readonly IIdentityService identityService;

        public PeekController(
            IDds dds,
            ICatalogue catalogue,
            IWorkStorageFactory workStorageFactory,
            ILogger<PeekController> logger,
            IIIIFBuilder iiifBuilder,
            LinkRewriter linkRewriter,
            IIdentityService identityService)
        {
            this.dds = dds;
            this.catalogue = catalogue;
            this.workStorageFactory = workStorageFactory;
            this.logger = logger;
            this.iiifBuilder = iiifBuilder;
            this.linkRewriter = linkRewriter;
            this.identityService = identityService;
        }

        private ContentResult IIIFContent(string json)
        {
            if (json.IsNullOrWhiteSpace())
            {
                json = "{\"error\": \"No content to serve\" }";
            }
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            if (linkRewriter.RequiresRewriting())
            {
                json = linkRewriter.RewriteLinks(json);
            }
            return Content(json, "application/json");
        }

        [AllowAnonymous]
        public async Task<ContentResult> IIIFRaw(string id, bool all = false)
        {
            var ddsId = identityService.GetIdentity(id); // full manifestation id, e.g., b19974760_233_0024
            var build = await BuildResult(ddsId, all);
            build.IIIFResource?.EnsurePresentation3Context();
            return IIIFContent(build.IIIFResource?.AsJson());
        }
        
        [AllowAnonymous]
        public async Task<ContentResult> IIIF2Raw(string id, bool all = false)
        {
            var ddsId = identityService.GetIdentity(id); // full manifestation id, e.g., b19974760_233_0024
            var build = await BuildResult(ddsId, all);
            if (build.MayBeConvertedToV2)
            {
                var iiif2 = iiifBuilder.BuildLegacyManifestations(ddsId, new[] { build });
                return IIIFContent(iiif2[id]?.IIIFResource?.AsJson());
            }

            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return Content("Contains AV, not going to convert to V2", "text/plain");
        }

        public async Task<ActionResult> IIIF(string id, bool all = false)
        {
            var ddsId = identityService.GetIdentity(id); // full manifestation id, e.g., b19974760_233_0024
            var build = await BuildResult(ddsId, all);
            build.IIIFResource?.EnsurePresentation3Context();
            var model = new CodeModel
            {
                Title = "IIIF Resource Preview",
                Description = "This has been built on the fly - it won't have been written to S3 yet.",
                Identifier = ddsId,
                RelativePath = ddsId.Value, // ?
                CodeAsString = build.IIIFResource?.AsJson(),
                ErrorMessage = build.Message,
                Mode = "ace/mode/json",
                Raw = Url.Action("IIIFRaw", new {id}),
                IncludeLinksToFullBuild = ddsId.Source == Source.Sierra
            };
            return View("Code", model);
        }

        /// <summary>
        /// If no document path has been supplied, redirect to the appropriate root document
        /// </summary>
        /// <param name="store"></param>
        /// <param name="parts"></param>
        /// <returns></returns>
        private static string GetRedirectPath(IWorkStore store, string parts)
        {
            if (parts.HasText()) return null;
            var rootDocument = store.GetRootDocument();
            return rootDocument.HasText() ? rootDocument : null;
        }

        public async Task<ActionResult> XmlRaw(string id, string parts)
        {
            var ddsId = identityService.GetIdentity(id);
            var store = await workStorageFactory.GetWorkStore(ddsId);
            var redirect = GetRedirectPath(store, parts);
            if (redirect.HasText())
            {
                return RedirectToAction("XmlRaw", new {id, parts = redirect});
            }
            var xmlSource = await store.LoadXmlForPath(parts);
            return Content(xmlSource.XElement.ToString(), "text/xml");
        }
        
        public async Task<ActionResult> XmlView(string id, string parts)
        {
            var ddsId = identityService.GetIdentity(id);
            var store = await workStorageFactory.GetWorkStore(ddsId);            
            var redirect = GetRedirectPath(store, parts);
            if (redirect.HasText())
            {
                return RedirectToAction("XmlView", new {id, parts = redirect});
            }
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

            DdsIdentity manifestationIdentifier = null;
            if (ddsId.Source == Source.Sierra)
            {
                try
                {
                    // we need to get a manifestation ID from just a file path which might be alto etc.
                    var manifestation = parts.Split('/').Last().Split('.')[0];
                    var bParts = manifestation.Split('_');
                    if (bParts.Length == 3)
                    {
                        manifestation = $"{bParts[0]}_{bParts[1]}";
                    }

                    manifestationIdentifier = identityService.GetIdentity(manifestation);
                }
                catch
                {
                    // ignored
                }
            }

            var model = new CodeModel
            {
                Title = "XML File View",
                Description = $"You can view other XML resources for {id} by changing the URL of this page.",
                Identifier = manifestationIdentifier ?? ddsId,
                RelativePath = parts,
                CodeAsString = xmlAsString,
                ErrorMessage = errorMessage,
                Mode = "ace/mode/xml",
                Raw = Url.Action("XmlRaw", new {id, parts}),
                AnchorFile = store.GetRootDocument()
            };
            
            return View("Code", model);
        }
        
        public async Task<ContentResult> StorageManifestRaw(string id)
        {
            var ddsId = identityService.GetIdentity(id);
            var archiveStore = (ArchiveStorageServiceWorkStore) await workStorageFactory.GetWorkStore(ddsId);
            var storageManifest = await archiveStore.GetStorageManifest();
            return Content(storageManifest.ToString(Formatting.Indented), "application/json");
        }
        
        public async Task<ActionResult> StorageManifest(string id)
        {
            var ddsId = identityService.GetIdentity(id);
            var archiveStore = (ArchiveStorageServiceWorkStore) await workStorageFactory.GetWorkStore(ddsId);

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
                Identifier = ddsId,
                CodeAsString = jsonAsString ?? string.Empty,
                ErrorMessage = errorMessage,
                Mode = "ace/mode/json",
                Raw = Url.Action("StorageManifestRaw", new {id}),
                AnchorFile = archiveStore.GetRootDocument()
            };
            return View("Code", model);
        }
        
        private async Task<BuildResult> BuildResult(DdsIdentity ddsId, bool all)
        {
            var results = await BuildIIIF(ddsId, all);
            var build = results[ddsId.Value];
            if (build is { RequiresMultipleBuild: true } && all == false)
            {
                var packageId = identityService.GetIdentity(ddsId.PackageIdentifier);
                results = await BuildIIIF(packageId, true);
                // do we still have the same resource in the results?
                // This particular manifestation might have been removed.
                // e.g., AV MM rearranged into one Manifest
                // So return the b number
                build = results[ddsId.Value] ?? results[ddsId.PackageIdentifier];
            }
            return build;
        }
        
        private async Task<MultipleBuildResult> BuildIIIF(DdsIdentity ddsId, bool all)
        {
            var work = await catalogue.GetWorkByOtherIdentifier(ddsId.PackageIdentifier);
            var packageId = identityService.GetIdentity(ddsId.PackageIdentifier);
            await dds.RefreshManifestations(packageId, work);
            if (all)
            {
                return await iiifBuilder.BuildAllManifestations(ddsId);
            }
            return await iiifBuilder.Build(ddsId, work);
        }
    }
}