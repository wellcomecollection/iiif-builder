using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviour : IProcessingBehaviour
{
    public HashSet<DeliveryChannel> DeliveryChannels { get; }

    public AssetFamily AssetFamily { get; }

    private Dictionary<string, Size>? videoSizesByChannel;

    public ProcessingBehaviour(StoredFile storedFile, ProcessingBehaviourOptions options)
    {
        if (storedFile.MimeType.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("A storedFile must have a mime type to deduce processingBehaviour");
        }
        DeliveryChannels = new HashSet<DeliveryChannel>();
        string? videoDefault = options.UseNamedAVDefaults ? "video-max" : null;
        string? audioDefault = options.UseNamedAVDefaults ? "audio-max" : null;
        
        // Images:
        if (storedFile.MimeType.IsImageMimeType())
        {
            var specificFormat = storedFile.MimeType.SplitByDelimiter('/')!.Last().ToLowerInvariant();
            DeliveryChannel[] deliveryChannels = options.ImageDeliveryChannels.ContainsKey(specificFormat) 
                ? options.ImageDeliveryChannels[specificFormat] 
                : options.DefaultImageDeliveryChannels;

            foreach (var deliveryChannel in deliveryChannels)
            {
                DeliveryChannels.Add(deliveryChannel);
            }
            
            if (DeliveryChannels.Any(dc => dc.Channel == ChannelNames.IIIFImage))
            {
                AssetFamily = AssetFamily.Image;
            }
            else
            {
                AssetFamily = AssetFamily.File;
            }
        }

        // Audio:
        else if (storedFile.MimeType.IsAudioMimeType())
        {
            AssetFamily = AssetFamily.TimeBased;
            if (storedFile.MimeType is "audio/mp3" or "audio/x-mpeg-3")
            {
                DeliveryChannels.Add(Channels.File());
            }
            else
            {
                DeliveryChannels.Add(Channels.IIIFAv(audioDefault));
            }
        }

        // Video:
        else if (storedFile.MimeType.IsVideoMimeType())
        {
            AssetFamily = AssetFamily.TimeBased;
            var height = storedFile.AssetMetadata?.GetMediaDimensions().Height;

            if (storedFile.MimeType == "video/mp4" &&
                storedFile.PhysicalFile!.Files!.Exists(f => f.MimeType == "application/mxf"))
            {
                // At the moment we are saying that if this MP4 accompanies an MXF master,
                // then it is the access copy and we can use it as-is.
                if (height <= options.MaxUntranscodedAccessMp4 || options.MakeAllAccessMP4sAvailable)
                {
                    DeliveryChannels.Add(Channels.File());
                }

                if (options.MaxUntranscodedAccessMp4 > 0 && height > options.MaxUntranscodedAccessMp4)
                {
                    // We still want to transcode it into one or more delivery versions.
                    // e.g., if it is a 4K video. So for some mp4s we'd not return "none" here.
                    // We also might decide to send the MXF instead or as well - in which case we need to 
                    // ignore physicalFile.MimeType and look at the actual IStoredFile.
                    DeliveryChannels.Add(Channels.IIIFAv());
                }
            }
            else
            {
                // it's not an MXF access MP4, or it's some other non-MP4 video, so we're always going to transcode it:
                DeliveryChannels.Add(Channels.IIIFAv());
            }

            var avDeliveryChannel = DeliveryChannels.SingleOrDefault(dc => dc.Channel == ChannelNames.IIIFAv);
            if(avDeliveryChannel != null)
            {
                // This assumes only one video is produced, but keeps the knowledge that
                // the default AV IOP creates 720p videos confined to this ProcessingBehaviour implementation.
                
                videoSizesByChannel = new Dictionary<string, Size>(1)
                {
                    [ChannelNames.IIIFAv] = new (1280, 720)
                };

                // We need to set the ImageOptimsationPolicy
                // At the moment this always returns videoDefault but the logic is here to do other things.
                // But what policy are we going to pick? The following allows that to be based on resolution:
                switch (height)
                {
                    case null or <= 0 or > 5000:
                        // Have a max to catch any weird values.
                        avDeliveryChannel.Policy = videoDefault;
                        break;
                    case <= 720:
                        // dlcs default (probably 720p). 
                        avDeliveryChannel.Policy = videoDefault;
                        break;
                    case <= 1080:
                        // HD (1920 x 1080)
                        avDeliveryChannel.Policy = videoDefault; // "HD";
                        break;
                    case <= 1440:
                        // QHD (2560 x 1440) or 2K (2048 x 1080)
                        avDeliveryChannel.Policy = videoDefault; // "QHD";  
                        break;

                    // and so on, and so on: 4K (3840 x 2160), 8K (7680 x 4320)

                    default:
                        // if here, height is between 1441 and 5000, so maybe use the 1440 setting?
                        // Or send to an HLS setting that will produce a variety of outputs.
                        avDeliveryChannel.Policy = videoDefault; // "HIGHER";
                        break;
                }
            }
        }

        // Files:
        else
        {
            AssetFamily = AssetFamily.File;
            DeliveryChannels.Add(Channels.File("none"));
        }
    }

    public Size? GetVideoSize(string deliveryChannel)
    {
        return videoSizesByChannel?[deliveryChannel];
    }
}