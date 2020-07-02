using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.Dashboard.Models
{
    public class TestModel
    {
        public string Message { get; set; }
        public IDigitisedManifestation Manifestation { get; set; }
        public SyncOperation SyncOperation { get; set; }
        public int DefaultSpace { get; set; }
        const string PortalPageTemplate = "https://portal.dlcs.io/Image.aspx?space={0}&image={1}";


        public Image GetDlcsImage(string storageIdentifier)
        {
            if (SyncOperation.ImagesAlreadyOnDlcs.ContainsKey(storageIdentifier))
            {
                return SyncOperation.ImagesAlreadyOnDlcs[storageIdentifier];
            }
            return null;
        }

        public string GetPortalPageForImage(Image image)
        {
            int? space = image.Space ?? DefaultSpace;
            return string.Format(PortalPageTemplate, space, image.StorageIdentifier);
        }

        public Thumbnail GetThumbnail(Image dlcsImage, int boundingSize)
        {
            if (dlcsImage == null || !dlcsImage.Width.HasValue || !dlcsImage.Height.HasValue)
            {
                return new Thumbnail
                {
                    Width = boundingSize,
                    Height = boundingSize,
                    Src = "placeholder.png"
                };
            }
            return dlcsImage.GetThumbnail(100, boundingSize, DefaultSpace);
        }

        public string GetIIIFImageService(Image dlcsImage, string imType)
        {
            if (dlcsImage == null) return string.Empty;
            return dlcsImage.GetIIIFImageService(imType, DefaultSpace);
        }

    }
}
