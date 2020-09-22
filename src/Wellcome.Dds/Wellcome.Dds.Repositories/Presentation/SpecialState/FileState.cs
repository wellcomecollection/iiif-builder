using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// Stores found files as it builds IIIF.
    /// That is - not AV, not images.
    /// These are either AV transcripts, or they are Born Digital PDFs.
    /// </summary>
    public class FileState
    {
        public List<IPhysicalFile> FoundFiles { get; set; }
        
        public FileState()
        {
            FoundFiles = new List<IPhysicalFile>();
        }
        
    }
}