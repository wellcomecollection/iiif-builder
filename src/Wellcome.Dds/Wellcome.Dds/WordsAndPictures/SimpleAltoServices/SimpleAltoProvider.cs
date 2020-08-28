﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    public class SimpleAltoProvider
    {
        private static XNamespace ns = "http://www.loc.gov/standards/alto/ns-v2#";

        public AnnotationPage GetAnnotationPage(XElement altoRoot, int actualWidth, int actualHeight, int index)
        {
            var textLines = new List<TextLine>();
            var illustrations = new List<Illustration>();
            var composedBlocks = new List<Illustration>();

            //var altoRoot = XElement.Load(fullAltoPath);
            var pageElement = altoRoot.Element(ns + "Layout").Element(ns + "Page");
            int srcW = Convert.ToInt32(pageElement.GetRequiredAttributeValue("WIDTH"));
            int srcH = Convert.ToInt32(pageElement.GetRequiredAttributeValue("HEIGHT"));
            float scaleW = (float)actualWidth / (float)srcW;
            float scaleH = (float)actualHeight / (float)srcH;
            // only get strings in textblocks, not page numbers and headers
            var printSpace = altoRoot.Descendants(ns + "PrintSpace").First();
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
            return new AnnotationPage
            {
                TextLines = textLines.ToArray(),
                Illustrations = illustrations.ToArray(),
                ComposedBlocks = composedBlocks.ToArray(),
                Index = index
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
