using System.Collections.Generic;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviour : IProcessingBehaviour
{
    public HashSet<string> DeliveryChannels { get; }
    public string? ImageOptimisationPolicy { get; }

    public ProcessingBehaviour(StoredFile storedFile, ProcessingBehaviourOptions options)
    {
        DeliveryChannels = new HashSet<string>();
        string? videoDefault = options.UseNamedAVDefaults ? "video-max" : null;
        string? audioDefault = options.UseNamedAVDefaults ? "audio-max" : null;
        
        // current DLCS defaults for IOP are simply `video-max` and `audio-max`, which will be chosen by the DLCS
        // if we leave ImageOptimisationPolicy empty.

        // Images:
        if (storedFile.MimeType.IsImageMimeType())
        {
            DeliveryChannels.Add("iiif-img");
            if (options.AddThumbsAsSeparateChannel)
            {
                DeliveryChannels.Add("thumbs");
            }
            if (options.MakeAllSourceImagesAvailable)
            {
                DeliveryChannels.Add("file");
            }

            if (storedFile.MimeType == "image/jp2")
            {
                ImageOptimisationPolicy = "use-original";
                if (options.MakeJP2Available || options.MakeAllSourceImagesAvailable)
                {   
                    DeliveryChannels.Add("file");
                }
            }
        }

        // Audio:
        else if (storedFile.MimeType.IsAudioMimeType())
        {
            if (storedFile.MimeType is "audio/mp3" or "audio/x-mpeg-3")
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
        else if (storedFile.MimeType.IsVideoMimeType())
        {
            var height = storedFile.AssetMetadata?.GetMediaDimensions().Height;

            if (storedFile.MimeType == "video/mp4" &&
                storedFile.PhysicalFile!.Files!.Exists(f => f.MimeType == "application/mxf"))
            {
                // At the moment we are saying that if this MP4 accompanies an MXF master,
                // then it is the access copy and we can use it as-is.
                if (height <= options.MaxUntranscodedAccessMp4 || options.MakeAllAccessMP4sAvailable)
                {
                    ImageOptimisationPolicy = "none"; // this IOP is a dummy value, really
                    DeliveryChannels.Add("file");
                }

                if (options.MaxUntranscodedAccessMp4 > 0 && height > options.MaxUntranscodedAccessMp4)
                {
                    // We still want to transcode it into one or more delivery versions.
                    // e.g., if it is a 4K video. So for some mp4s we'd not return "none" here.
                    // We also might decide to send the MXF instead or as well - in which case we need to 
                    // ignore physicalFile.MimeType and look at the actual IStoredFile.
                    DeliveryChannels.Add("iiif-av");
                }
            }
            else
            {
                // it's not an MXF access MP4, or it's some other non-MP4 video, so we're always going to transcode it:
                DeliveryChannels.Add("iiif-av");
            }

            if (DeliveryChannels.Contains("iiif-av"))
            {
                // We need to set the ImageOptimsationPolicy
                // At the moment this always returns videoDefault but the logic is here to do other things.
                // But what policy are we going to pick? The following allows that to be based on resolution:
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