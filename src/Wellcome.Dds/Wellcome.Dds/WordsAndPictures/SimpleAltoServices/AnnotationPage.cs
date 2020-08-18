using System;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    public class AnnotationPage
    {
        public int Index { get; set; }
        public TextLine[] TextLines { get; set; }
        public Illustration[] Illustrations { get; set; }
        public Illustration[] ComposedBlocks { get; set; }
    }
}
