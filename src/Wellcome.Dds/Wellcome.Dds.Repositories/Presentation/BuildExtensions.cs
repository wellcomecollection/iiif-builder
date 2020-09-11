using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.Content;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class BuildExtensions
    {
        private static readonly int[] ThumbSizes = { 1024, 400, 200, 100 };
        
        public static List<Size> GetThumbSizes(this string metadataString)
        {
            if (metadataString.HasText())
            {
                var allSizes = JsonConvert.DeserializeObject<List<int[]>>(metadataString);
                return allSizes.Skip(1)
                    .Select(s => new Size(s[0], s[1]))
                    .ToList();
            }
            return new List<Size>();
        }

        public static Size GetActualSize(this string metadataString)
        {
            if (metadataString.HasText())
            {
                var allSizes = JsonConvert.DeserializeObject<List<int[]>>(metadataString);
                return new Size(allSizes[0][0], allSizes[0][1]);
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
        public static List<ExternalResource> GetThumbnail(
            this List<Manifestation> manifestations, 
            string digitisedManifestationIdentifier)
        {
            var manifestation = manifestations.FirstOrDefault(
                mf => mf.ManifestationIdentifier == digitisedManifestationIdentifier);
            if (manifestation == null)
            {
                return null;
            }
            var thumbSource = manifestation.CatalogueThumbnail;
            var sizeSource = manifestation.CatalogueThumbnailDimensions;
            if (!thumbSource.HasText())
            {
                thumbSource = manifestation.FirstFileThumbnail;
                sizeSource = manifestation.FirstFileThumbnailDimensions;
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
            var largest = thumbSizes.First();
            var smallest = thumbSizes.Last();
            var thumb = new Image
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
            return new List<ExternalResource>{ thumb };
        }
        
    }
}