﻿using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Models
{
    public class CodeModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DdsIdentifier Identifier { get; set; }
        public string AnchorFile { get; set; }
        public string RelativePath { get; set; }
        public string CodeAsString { get; set; }
        public string ErrorMessage { get; set; }
        public string Mode { get; set; }
        public string Raw { get; set; }
        public bool IncludeLinksToFullBuild { get; set; }
    }
}