using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Image = IIIF.Presentation.V3.Content.Image;
using Size = IIIF.Size;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class BuildExtensions
    {
        public static readonly int[] ThumbSizes = { 1024, 400, 200, 100 };
        
        public static List<Size> GetThumbSizes(this string metadataString)
        {
            if (metadataString.HasText())
            {
                var allSizes = JsonConvert.DeserializeObject<List<int[]>>(metadataString);
                if (allSizes != null)
                {
                    return allSizes.Skip(1)
                        .Select(s => new Size(s[0], s[1]))
                        .ToList();
                }
            }
            return new List<Size>();
        }

        public static Size? GetActualSize(this string metadataString)
        {
            if (metadataString.HasText())
            {
                var allSizes = JsonConvert.DeserializeObject<List<int[]>>(metadataString);
                if (allSizes != null)
                {
                    return new Size(allSizes[0][0], allSizes[0][1]);
                }
            }
            return null;
        }

        public static string GetAvailableSizeAsString(this IPhysicalFile asset)
        {
            var sizes = asset.GetAvailableSizes();
            return JsonConvert.SerializeObject(sizes.Select(s => new []{s.Width,s.Height}.ToList()));
        }

        public static List<Size> GetAvailableSizes(this IPhysicalFile asset)
        {
            var sizes = new List<Size>();
            var actualSize = new Size(
                asset.AssetMetadata.GetImageWidth(),
                asset.AssetMetadata.GetImageHeight());
            sizes.Add(new Size(actualSize.Width, actualSize.Height));
            var usableThumbs = new List<int>();
            switch (asset.AccessCondition)
            {
                case AccessCondition.Open:
                    usableThumbs = ThumbSizes.ToList();
                    break;
                case AccessCondition.RequiresRegistration:
                case AccessCondition.OpenWithAdvisory:
                    usableThumbs = ThumbSizes.Skip(2).ToList();
                    break;
            }
            foreach (int thumbSize in usableThumbs)
            {
                var confinedSize = Size.Confine(thumbSize, actualSize);
                sizes.Add(new Size(confinedSize.Width, confinedSize.Height));
            }
            return sizes;
        }
        
        // TODO - this can be used for canvas thumbs, too, as an extension for IPhysicalFile.. although different enough...
        // TODO - needs take into account access conditions - but have we already done that in Synchroniser? YES!
        
        // still need posters:
        // still need PDF thumbs
        // https://github.com/wellcomelibrary/dds-ecosystem/blob/new-storage-service/wellcome-dds/Wellcome.Dds/LinkedData/LodProviders/PackageTripleProvider.cs#L131
        public static List<ExternalResource>? GetThumbnail(
            this List<Manifestation> manifestations, 
            string digitisedManifestationIdentifier)
        {
            var manifestation = manifestations.FirstOrDefault(
                mf => mf.ManifestationIdentifier == digitisedManifestationIdentifier);
            if (manifestation == null)
            {
                return null;
            }
            return manifestation.GetThumbnail();
        }

        public static List<ExternalResource>? GetThumbnail(this Manifestation manifestation)
        {
            var thumbSource = manifestation.CatalogueThumbnail;
            var sizeSource = manifestation.CatalogueThumbnailDimensions;
            if (!thumbSource.HasText())
            {
                thumbSource = manifestation.FirstFileThumbnail;
                sizeSource = manifestation.FirstFileThumbnailDimensions;
            }

            if (thumbSource.IsNullOrWhiteSpace() || sizeSource.IsNullOrWhiteSpace())
            {
                return null;
            }
            if (!StringUtils.AllHaveText(thumbSource, sizeSource))
            {
                return null;
            }
            var thumbSizes = sizeSource.GetThumbSizes();
            if (!thumbSizes.HasItems())
            {
                return null;
            }
            return new List<ExternalResource>
            {
                thumbSource.AsThumbnailWithService(thumbSizes)
            };
        }

        public static Image AsThumbnailWithService(this string thumbSource, List<Size> thumbSizes)
        {
            // These are in descending size order
            var largest = thumbSizes.First();
            var smallest = thumbSizes.Last();
            return new Image
            {
                Id = $"{thumbSource}/full/{smallest.Width},{smallest.Height}/0/default.jpg",
                Width = smallest.Width,
                Height = smallest.Height,
                Service = new List<IService>
                {
                    new ImageService2
                    {
                        Id = thumbSource,
                        Width = largest.Width,
                        Height = largest.Height,
                        Profile = ImageService2.Level0Profile,
                        Sizes = thumbSizes.AsEnumerable().Reverse().ToList()
                    }
                }
            };
        }

        public static Image AsImageWithService(this string imageSource, Size actualSize, Size staticSize)
        {
            return new Image
            {
                Id = $"{imageSource}/full/{staticSize.Width},{staticSize.Height}/0/default.jpg",
                Width = staticSize.Width,
                Height = staticSize.Height,
                Format = "image/jpeg",
                Service = new List<IService>
                {
                    new ImageService2
                    {
                        Id = imageSource,
                        Width = actualSize.Width,
                        Height = actualSize.Height,
                        Profile = ImageService2.Level1Profile
                    }
                }
            };
        }

        public static string? GetLocationOfOriginal(this List<Metadata> metadata)
        {
            var locationOfOriginal = metadata
                .FirstOrDefault(m => m.Label == "Location");
            return locationOfOriginal?.StringValue;
        }
        
        public static IEnumerable<string> GetDigitalCollectionCodes(this List<Metadata> metadata)
        {
            return metadata
                .Where(m => m.Label == "Digitalcollection")
                .Select(m => m.Identifier);
        }

        public static string WrapSpan(this string s)
        {
            return $"<span>{s}</span>";
        }

        public static bool SupportsSearch(this IEnumerable<IPhysicalFile> assets)
        {
            return assets.Any(pf => pf.RelativeAltoPath.HasText());
        }

        public static bool IsMultiPart(this ResourceBase resource)
        {
            if (resource.Behavior.HasItems())
            {
                return resource.Behavior.Contains(Behavior.MultiPart);
            }
            return false;
        }
        
        /// <summary>
        /// Is there more than one copy of the work? 
        /// </summary>
        public static bool IsMultiPart(this IManifestation manifestation)
        {
            switch (manifestation.Type)
            {
                case "MultipleVolume":
                case "MultipleCopy":
                case "MultipleVolumeMultipleCopy":
                    return true;
                default:
                    return false;
            }
        }


        public static Size? GetWhSize(this IPhysicalFile file)
        {
            var dimensions = file.AssetMetadata?.GetMediaDimensions();
            if (dimensions == null) return null;
            
            var w = dimensions.Width.GetValueOrDefault();
            var h = dimensions.Height.GetValueOrDefault();
            if (w > 0 && h > 0)
            {
                return new Size(w, h);
            }

            return null;
        }
    }
}