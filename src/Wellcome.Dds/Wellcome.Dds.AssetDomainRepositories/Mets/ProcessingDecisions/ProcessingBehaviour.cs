using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviour : IProcessingBehaviour
{
    public HashSet<string> DeliveryChannels { get; }
    public string? ImageOptimisationPolicy { get; }

    public ProcessingBehaviour(PhysicalFile physicalFile, bool useNamedAVDefaults)
    {
        DeliveryChannels = new HashSet<string>();
        string? videoDefault = useNamedAVDefaults ? "video-max" : null;
        string? audioDefault = useNamedAVDefaults ? "audio-max" : null;
        
        // current DLCS defaults for IOP are simply `video-max` and `audio-max`, which will be chosen by the DLCS
        // if we leave ImageOptimisationPolicy empty.

        // Images:
        if (physicalFile.MimeType.IsImageMimeType())
        {
            DeliveryChannels.Add("iiif-img");
            DeliveryChannels.Add("thumbs");
            // deliveryChannels.Add("file"); // if we want to make ALL images available as original

            if (physicalFile.MimeType == "image/jp2")
            {
                ImageOptimisationPolicy = "use-original";
                // deliveryChannels.Add("file"); // if we want to make the original JP2 available
            }
        }

        // Audio:
        else if (physicalFile.MimeType.IsAudioMimeType())
        {
            if (physicalFile.MimeType is "audio/mp3" or "audio/x-mpeg-3")
            {
                ImageOptimisationPolicy = "none";
                DeliveryChannels.Add("file");
            }
            else
            {
                DeliveryChannels.Add("iiif-av");
                ImageOptimisationPolicy = audioDefault;
            }
        }

        // Video:
        else if (physicalFile.MimeType.IsVideoMimeType())
        {
            if (physicalFile.MimeType == "video/mp4" &&
                physicalFile.Files!.Exists(f => f.MimeType == "application/mxf"))
            {
                // At the moment we are saying that if this MP4 accompanies an MXF master,
                // then it is the access copy and we should just use it as-is.
                // Later, we may decide that we still want to transcode it into one or more delivery versions.
                // e.g., if it is a 4K video. So for some mp4s we'd not return "none" here.
                // We also might decide to send the MXF instead or as well - in which case we need to 
                // ignore physicalFile.MimeType and look at the actual IStoredFile.
                ImageOptimisationPolicy = "none";
                DeliveryChannels.Add("file");
            }
            else
            {
                // it's not an MXF access MP4, or it's some other non-MP4 video, so we're going to transcode it:
                DeliveryChannels.Add("iiif-av");
                
                // But what policy are we going to pick? The following allows that to be based on resolution:
                int? height = physicalFile.AssetMetadata?.GetMediaDimensions().Height;
                switch (height)
                {
                    case null or <= 0 or > 5000:
                        // Have a max to catch any weird values.
                        ImageOptimisationPolicy = videoDefault;  
                        break;
                    case <= 720:
                        // dlcs default (probably 720p). 
                        ImageOptimisationPolicy = videoDefault;
                        break;
                    case <= 1080:
                        // HD (1920 x 1080)
                        ImageOptimisationPolicy = videoDefault; // "HD";
                        break;
                    case <= 1440:
                        // QHD (2560 x 1440) or 2K (2048 x 1080)
                        ImageOptimisationPolicy = videoDefault; // "QHD";  
                        break;
                    
                    // and so on, and so on: 4K (3840 x 2160), 8K (7680 x 4320)
                    
                    default:
                        // if here, height is between 1441 and 5000, so maybe use the 1440 setting?
                        // Or send to an HLS setting that will produce a variety of outputs.
                        ImageOptimisationPolicy = videoDefault; // "HIGHER";
                        break;
                }
            
            }
        }

        // Files:
        else
        {
            ImageOptimisationPolicy = "none";
            DeliveryChannels.Add("file");
        }
    }
}