using System;
using System.Collections.Generic;
using System.Text;
using IIIF.Presentation;

namespace Wellcome.Dds.IIIFBuilding
{
    public class BuildResult
    {
        public BuildOutcome Outcome { get; set; }
        public string Message { get; set; }
        public string IIIF3Key { get; set; }
        public StructureBase IIIF3Resource { get; set; }
        
        
        // TODO - this won't be a StructureBase
        public string IIIF2Key { get; set; }
        public StructureBase IIIF2Resource { get; set; }
        
    }
}
