using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    /// <summary>
    /// Each job must be one IIIF manifest.
    /// The DLCS Ingest Job is then picked up as a DLCS _reconciliation_ job
    /// </summary>
    [Index(nameof(Identifier))]
    [Index(nameof(Created))]
    public class DlcsIngestJob
    {
        public int Id { get; set; } 
        public DateTime Created { get; set; }
        public string? Identifier { get; set; }
        public string? Label { get; set; }
        public int SequenceIndex { get; set; }
        public string? VolumePart { get; set; }
        public string? IssuePart { get; set; }
        public int ImageCount { get; set; }
        public DateTime? StartProcessed { get; set; }
        public DateTime? EndProcessed { get; set; }
        public string? AssetType { get; set; }

        public bool Succeeded { get; set; }
        public string? Data { get; set; }
        public int ReadyImageCount { get; set; }

        public virtual ICollection<DlcsBatch> DlcsBatches { get; set; } = null!;

        /// <summary>
        /// Determine the field of this job that is equivalent to a manifest identifier
        /// </summary>
        /// <returns></returns>
        public string? GetManifestationIdentifier()
        {
            if (!string.IsNullOrWhiteSpace(IssuePart))
            {
                return IssuePart;
            }
            if (!string.IsNullOrWhiteSpace(VolumePart))
            {
                return VolumePart;
            }
            return Identifier;
        }

        public override string ToString()
        {
            return string.Format("[DlcsIngestJob {0} for {1}/{2}|{3}|{4}|{5}|{6}]",
                Id, Identifier, SequenceIndex, VolumePart, IssuePart, AssetType, Created);
        }
    }
}
