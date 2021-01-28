using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DlcsWebClient.Config;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Utils;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Models
{
    public class ManifestationModel
    {
        const string PortalPageTemplate = "https://portal.dlcs.io/Image.aspx?space={0}&image={1}";
        const string PortalBatchTemplate = "https://portal.dlcs.io/Batch.aspx?batch={0}";
        const string GlyphTemplate = "<span class=\"glyphicon glyphicon-{0}\"></span>";

        private static readonly char[] SlashSeparator = new[] { '/' };
        public DdsIdentifier DdsIdentifier { get; set; }
        public IDigitisedManifestation DigitisedManifestation { get; set; }
        public IDigitisedCollection Parent { get; set; }
        public IDigitisedCollection GrandParent { get; set; }
        public IUrlHelper Url { get; set; }
        public DlcsOptions DlcsOptions { get; set; }

        // if we want to use these...
        public ISimpleStoredFileInfo CachedPackageFileInfo { get; set; }
        public ISimpleStoredFileInfo CachedTextModelFileInfo { get; set; }
        public ISimpleStoredFileInfo CachedAltoAnnotationsFileInfo { get; set; }

        public string DlcsSkeletonManifest { get; set; }
        
        public bool IsStandardMultipleManifestation { get; set; }
        public string PreviousLink { get; set; }
        public string NextLink { get; set; }
        public List<IManifestation> Siblings { get; set; }
        public int Index { get; set; }
        public int DefaultSpace { get; set; }
        public List<DlcsIngestJob> IngestJobs { get; set; }

        public SyncOperation SyncOperation { get; set; }

        public List<Batch> BatchesForImages { get; set; }
        public Dictionary<int, List<Batch>> DbJobIdsToActiveBatches { get; set; }
        public bool IsRunning { get; set; }

        private SyncSummary syncSummary;
        public SyncSummary SyncSummary
        {
            get
            {
                if (syncSummary != null)
                {
                    return syncSummary;
                }
                syncSummary = new SyncSummary();
                var countsdict = new Dictionary<string, int>();
                var iconDict = new Dictionary<string, string>();
                var rowIdDict = new Dictionary<string, string>();
                var accessConditionCounts = new Dictionary<string, int>();
                var accessConditionLinks = new Dictionary<string, string>();
                syncSummary.CssClass = "info";
                foreach (var sf in DigitisedManifestation.MetsManifestation.SynchronisableFiles)
                {
                    if (IsIgnored(sf.StorageIdentifier))
                    {
                        continue;
                    }
                    var dlcsImage = GetDlcsImage(sf.StorageIdentifier);
                    var desc = GetStatusDescriptionForImage(dlcsImage);
                    if (!countsdict.ContainsKey(desc))
                    {
                        countsdict[desc] = 0;
                        iconDict[desc] = GetStatusIconForImageRow(dlcsImage, false);
                        rowIdDict[desc] = GetTableId(sf.StorageIdentifier);
                    }
                    countsdict[desc] = countsdict[desc] + 1;
                    if (desc == "error" || desc == "missing")
                    {
                        syncSummary.CssClass = "danger";
                    }
                    if (syncSummary.CssClass != "danger" && desc == "metadata mismatch")
                    {
                        syncSummary.CssClass = "warning";
                    }
                    if (!accessConditionCounts.ContainsKey(sf.PhysicalFile.AccessCondition))
                    {
                        accessConditionCounts[sf.PhysicalFile.AccessCondition] = 0;
                        accessConditionLinks[sf.PhysicalFile.AccessCondition] = GetTableId(sf.StorageIdentifier);
                    }
                    accessConditionCounts[sf.PhysicalFile.AccessCondition] = accessConditionCounts[sf.PhysicalFile.AccessCondition] + 1;
                }
                syncSummary.Categories = countsdict.Select(kvp => new SyncCategory
                {
                    Label = kvp.Key,
                    Count = kvp.Value,
                    StatusIcon = iconDict[kvp.Key],
                    TableRowId = rowIdDict[kvp.Key]
                })
                .ToArray();
                syncSummary.AccessConditions = accessConditionCounts.Select(kvp => new AccessSummary
                {
                    Label = kvp.Key,
                    Count = kvp.Value,
                    StatusIcon = GetAccessConditionIcon(kvp.Key),
                    TableRowId = accessConditionLinks[kvp.Key]
                })
                    .ToArray();
                return syncSummary;
            }
        }

        public AssetFamily ManifestationFamily
        {
            get
            {
                return DigitisedManifestation.MetsManifestation.FirstInternetType.GetAssetFamily();
            }
        }



        public string GetPortalPageForImage(Image image)
        {
            int? space = image.Space ?? DefaultSpace;
            return string.Format(PortalPageTemplate, space, image.StorageIdentifier);
        }
        public string GetPortalPageForBatch(Batch batch)
        {
            return string.Format(PortalBatchTemplate, batch.Id.Split(SlashSeparator).Last());
        }

        public void MakeManifestationNavData()
        {
            if (Parent != null) // && GrandParent == null)
            {
                IsStandardMultipleManifestation = true;
                Siblings = Parent.MetsCollection.Manifestations.ToList();
                PreviousLink = "#";
                NextLink = "#";
                for (int i = 0; i < Siblings.Count; i++)
                {
                    if (Siblings[i].Id == DdsIdentifier)
                    {
                        Index = i + 1;
                        if (i > 0)
                        {
                            PreviousLink = Url.Action("Manifestation", "Dash", new { id = Siblings[i - 1].Id });
                        }
                        if (i < Siblings.Count - 1)
                        {
                            NextLink = Url.Action("Manifestation", "Dash", new { id = Siblings[i + 1].Id });
                        }
                    }
                }
            }
        }

        public string FormatSize(string rawSize)
        {
            long asLong;
            if (long.TryParse(rawSize, out asLong))
            {
                return StringUtils.FormatFileSize(asLong);
            }
            return rawSize;
        }

        public string GetDropDownClass(IManifestation manifestation)
        {
            if (manifestation.Id == DigitisedManifestation.Identifier)
            {
                return "disabled";
            }
            return "";
        }

        public string GetAssetGlyph()
        {
            switch (DigitisedManifestation.MetsManifestation.FirstInternetType)
            {
                case "image/jp2":
                    return string.Format(GlyphTemplate, "picture");
                case "video/mp2":
                case "video/mp4":
                case "video/mpeg":
                    return string.Format(GlyphTemplate, "film");
                case "audio/mp3":
                case "audio/x-mpeg-3":
                case "audio/wav":
                case "audio/x-wav":
                case "audio/mpeg":
                    return string.Format(GlyphTemplate, "volume-up");
                default:
                    return string.Format(GlyphTemplate, "file");
            }
        }
        private string GetAccessConditionIcon(string accessCondition)
        {
            switch (accessCondition.ToLowerInvariant())
            {
                case "open":
                    return string.Format(GlyphTemplate, "heart");
                case "requires registration":
                    return string.Format(GlyphTemplate, "hand-up");
                case "clinical images":
                    return string.Format(GlyphTemplate, "sunglasses");
                case "restricted files":
                    return string.Format(GlyphTemplate, "warning-sign");
                case "closed":
                    return string.Format(GlyphTemplate, "remove");
                default:
                    return string.Format(GlyphTemplate, "question-sign");
            }
        }

        public Image GetDlcsImage(string storageIdentifier)
        {
            if (SyncOperation.ImagesAlreadyOnDlcs.ContainsKey(storageIdentifier))
            {
                return SyncOperation.ImagesAlreadyOnDlcs[storageIdentifier];
            }
            return null;
        }

        public bool IsIgnored(string storageIdentifier)
        {
            return SyncOperation.StorageIdentifiersToIgnore.Contains(storageIdentifier);
        }

        public string GetCssClassForImageRow(Image dlcsImage, bool ignored)
        {
            if (ignored) return "text-muted";
            if (dlcsImage == null) return "danger";
            if (dlcsImage.Ingesting == true) return "info";
            if (dlcsImage.Error.HasText()) return "warning";
            if (SyncOperation.DlcsImagesToIngest.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return "danger";
            if (SyncOperation.DlcsImagesToPatch.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return "warning";
            return "";
        }


        public string GetStatusIconForImageRow(Image dlcsImage, bool ignored)
        {
            if (ignored) return "";
            if (dlcsImage == null) return string.Format(GlyphTemplate, "ban-circle");
            if (dlcsImage.Ingesting == true) return string.Format(GlyphTemplate, "hourglass");
            if (dlcsImage.Error.HasText()) return string.Format(GlyphTemplate, "exclamation-sign");
            if (SyncOperation.DlcsImagesToIngest.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return string.Format(GlyphTemplate, "open");
            if (SyncOperation.DlcsImagesToPatch.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return string.Format(GlyphTemplate, "wrench");
            return "";
        }

        public string GetStatusDescriptionForImage(Image dlcsImage)
        {
            if (dlcsImage == null) return "missing";
            if (dlcsImage.Ingesting == true) return "ingesting";
            if (dlcsImage.Error.HasText()) return "error";
            if (SyncOperation.DlcsImagesToIngest.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return "upload";
            if (SyncOperation.DlcsImagesToPatch.Exists(im => im.StorageIdentifier == dlcsImage.StorageIdentifier))
                return "metadata mismatch";
            return "(OK)";
        }
        public string GetTableId(string storageIdentifier)
        {
            // this used to be a GUID
            // but now it might be a filename
            // either way it needs to be URL-safe
            var safeId = storageIdentifier.Replace("-", "");
            safeId = safeId.Replace(".", "");
            return string.Format("tb{0}", safeId);
        }

        public string GetBatchPercent(Batch batch)
        {
            return Math.Round(((double)batch.Completed / (double)batch.Count * 100)) + "%";
        }
        public string GetBatchErrorPercent(Batch batch)
        {
            return Math.Round(((double)batch.Errors / (double)batch.Count * 100)) + "%";
        }

        public string GetAbridgedRoles(string[] roles)
        {
            return string.Join(", ", roles.Select(r => r.Split(SlashSeparator).Last()));
        }

        public string GetDisplayMetadata(string metadata)
        {
            dynamic jmd = JObject.Parse(metadata);
            var html = new StringBuilder();
            if (jmd.transcodes != null)
            {
                foreach (var transcode in jmd.transcodes)
                {
                    html.AppendFormat("{0}: {1}, {2}<br/>",
                        transcode.format,
                        StringUtils.FormatFileSize((long)transcode.size),
                        transcode.duration);
                }
            }
            if (jmd.problems != null)
            {
                foreach (var problem in jmd.problems)
                {
                    html.AppendFormat("PROBLEMS: {0}<br/>", problem);
                }
            }
            return html.ToString();
        }

        public string GetIIIFImageService(Image dlcsImage, string imType)
        {
            if (dlcsImage == null) return string.Empty;
            int? space = dlcsImage.Space ?? DefaultSpace;
            const string template = "https://dlcs.io/{0}/{1}/{2}/{3}";
            return string.Format(template, imType, 2, space, dlcsImage.StorageIdentifier);
        }

        public string GetAwsConsoleUri(string fileUri)
        {
            const string template = "https://s3.console.aws.amazon.com/s3/object{0}?region=eu-west-1&tab=overview";
            var addrPos = fileUri.IndexOf("/", 9, StringComparison.Ordinal);
            var addr = fileUri.Substring(addrPos);
            return string.Format(template, addr);
        }

        public Thumbnail GetThumbnail(Image dlcsImage, int boundingSize)
        {    
            if (dlcsImage == null || !dlcsImage.Width.HasValue || !dlcsImage.Height.HasValue)
            {
                return new Thumbnail
                {
                    Width = boundingSize,
                    Height = boundingSize,
                    Src = "/dash/img/placeholder.png"
                };
            }
            var src = GetIIIFImageService(dlcsImage, "thumbs") + "/full/!100,100/0/default.jpg";
            if (dlcsImage.Width <= boundingSize && dlcsImage.Height <= boundingSize)
            {
                return new Thumbnail
                {
                    Width = boundingSize,
                    Height = boundingSize,
                    Src = src
                };
            }
            var scaleW = boundingSize / (double)dlcsImage.Width;
            var scaleH = boundingSize / (double)dlcsImage.Height;
            var scale = Math.Min(scaleW, scaleH);
            return new Thumbnail
            {
                Width = (int)Math.Round((dlcsImage.Width.Value * scale)),
                Height = (int)Math.Round((dlcsImage.Height.Value * scale)),
                Src = src
            };
        }

        public SimplifiedJson GetJsonModel()
        {
            var model = new SimplifiedJson
            {
                SyncOperation = SyncOperation,
                MetsManifestation = DigitisedManifestation.MetsManifestation,
                DlcsImages = DigitisedManifestation.DlcsImages,
                SequenceIndex = -1, // DigitisedManifestation.SequenceIndex,
                WorkStore = DigitisedManifestation.MetsManifestation.Sequence[0].WorkStore
            };
            foreach (var physicalFile in model.MetsManifestation.Sequence)
            {
                physicalFile.WorkStore = null;
            }
            return model;
        }


        public AVDerivative[] AVDerivatives { get; set; }
        public Work Work { get; set; }
        public string WorkPage { get; set; }
        public string CatalogueApi { get; set; }
        public string EncoreRecordUrl { get; set; }
        public string EncoreBiblioRecordUrl { get; set; }
        public string ManifestUrl { get; set; }

        /// <summary>
        /// return the additional (adjunct) files that need to be displayed in the dashboard
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public IEnumerable<IStoredFile> GetAdjunctsForDashboardDisplay(List<IStoredFile> files)
        {
            return files.Where(f => 
                f.Use != "OBJECTS" &&  // Old workflow
                f.Use != "ACCESS" &&   // New workflow
                f.Use != "ALTO");
        }
    }


    public class SimplifiedJson
    {
        public SyncOperation SyncOperation { get; set; }
        public IManifestation MetsManifestation { get; set; }
        public IEnumerable<Image> DlcsImages { get; set; }
        public int SequenceIndex { get; set; }
        public IWorkStore WorkStore { get; set; }
    }

    public class SimpleCollectionModel
    {
        public List<SimpleLink> Manifestations { get; set; }
        public List<SimpleLink> Collections { get; set; }
    }

    public class SimpleLink
    {
        public string Label { get; set; }
        public string Url { get; set; }
    }
}
