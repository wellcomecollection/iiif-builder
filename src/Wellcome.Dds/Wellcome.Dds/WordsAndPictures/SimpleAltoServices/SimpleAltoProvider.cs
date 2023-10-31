using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Utils;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    public class SimpleAltoProvider
    {
        private readonly ILogger? logger;
        private static readonly XNamespace ns = "http://www.loc.gov/standards/alto/ns-v2#";

        public SimpleAltoProvider()
        {
            logger = null;
        }
        
        public SimpleAltoProvider(ILogger logger)
        {
            this.logger = logger;
        }
        

        public AnnotationPage GetAnnotationPage(
            XElement? altoRoot,
            int actualWidth, int actualHeight,
            string manifestationIdentifier, string? assetIdentifier,
            int index)
        {
            var textLines = new List<TextLine>();
            var illustrations = new List<Illustration>();

            if (altoRoot != null)
            {
                try
                {
                    var pageElement = altoRoot.Element(ns + "Layout")!.Element(ns + "Page");
                    int srcW = Convert.ToInt32(pageElement!.GetRequiredAttributeValue("WIDTH"));
                    int srcH = Convert.ToInt32(pageElement!.GetRequiredAttributeValue("HEIGHT"));
                    float scaleW = actualWidth / (float)srcW;
                    float scaleH = actualHeight / (float)srcH;
                    // only get strings in textblocks, not page numbers and headers
                    var printSpace = altoRoot.Descendants(ns + "PrintSpace").FirstOrDefault();
                    if (printSpace != null)
                    {
                        foreach (var altoTextLine in printSpace.Descendants(ns + "TextLine"))
                        {
                            var textLine = new TextLine();
                            SetScaledDimensions(altoTextLine, textLine, scaleW, scaleH);
                            textLine.Text = string.Join(" ", altoTextLine.Descendants(ns + "String")
                                .Select(s => s.GetRequiredAttributeValue("CONTENT").Trim()));
                            textLines.Add(textLine);
                        }

                        foreach (var altoIllustration in printSpace.Descendants(ns + "Illustration"))
                        {
                            var illustration = new Illustration();
                            SetScaledDimensions(altoIllustration, illustration, scaleW, scaleH);
                            illustration.Type = altoIllustration.GetAttributeValue("TYPE", "Unknown");
                            illustrations.Add(illustration);
                        }

                        foreach (var altoComposedBlock in printSpace.Descendants(ns + "ComposedBlock"))
                        {
                            var composedBlock = new Illustration();
                            SetScaledDimensions(altoComposedBlock, composedBlock, scaleW, scaleH);
                            composedBlock.Type = altoComposedBlock.GetAttributeValue("TYPE", "Unknown");
                            illustrations.Add(composedBlock);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, 
                        "SimpleAltoProvider cannot extract page from ALTO for {assetIdentifier}", assetIdentifier);
                }
            }

            return new AnnotationPage
            {
                TextLines = textLines.ToArray(),
                Illustrations = illustrations.ToArray(),
                ComposedBlocks = Array.Empty<Illustration>(),
                ManifestationIdentifier = manifestationIdentifier,
                AssetIdentifier = assetIdentifier,
                Index = index,
                ActualWidth = actualWidth,
                ActualHeight = actualHeight
            };
        }

        private void SetScaledDimensions(XElement altoBlock, Block block, float scaleW, float scaleH)
        {
            block.X = (int) (Convert.ToInt32(altoBlock.GetRequiredAttributeValue("HPOS"))*scaleW);
            block.Y = (int) (Convert.ToInt32(altoBlock.GetRequiredAttributeValue("VPOS"))*scaleH);
            block.Width = (int) (Convert.ToInt32(altoBlock.GetRequiredAttributeValue("WIDTH"))*scaleW);
            block.Height = (int) (Convert.ToInt32(altoBlock.GetRequiredAttributeValue("HEIGHT"))*scaleH);
        }
    }
}
