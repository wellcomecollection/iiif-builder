using System;
using System.Collections.Generic;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public class DigitisedManifestation : BaseDigitisedResource, IDigitisedManifestation
    {
        public IManifestation MetsManifestation { get; set; }
        /// <summary>
        /// The images already on the DLCS for this manifestation
        /// </summary>
        public IEnumerable<Image> DlcsImages { get; set; }

        /// <summary>
        /// TODO: This doesn't belong here! Only here for PDF link to work and be same as in IIIF manifest
        /// </summary>
        public int SequenceIndex { get; set; }

        private IPdf pdf;
        public IPdf Pdf
        {
            get
            {
                if (pdf == null && PdfGetter != null)
                {
                    // TODO - change to use proper identifiers, not bnum/seq
                    pdf = PdfGetter(MetsManifestation.GetRootId(), SequenceIndex);
                }
                return pdf;
            }
        }

        public Func<string, int, IPdf> PdfGetter; 

        public string DlcsStatus { get; set; }
        public string DlcsResponse { get; set; }
        
        public bool JobExactMatchForManifestation(DlcsIngestJob job)
        {
            if (job.Identifier != MetsManifestation.GetRootId())
            {
                //Log.ErrorFormat("BNumber for job {0} does not match manifestation GetRootId() {1}",
                //    job.Identifier, MetsManifestation.GetRootId());
                return false;
            }
            if (job.IssuePart.HasText())
            {
                // a periodical issue
                if (!job.VolumePart.HasText() || !job.IssuePart.StartsWith(job.VolumePart))
                {
                    //Log.ErrorFormat("Job has issuePart {0} that does not match volumePart {1}",
                    //    job.IssuePart, job.VolumePart);
                    return false;
                }
                if (job.IssuePart != MetsManifestation.Id)
                {
                    //Log.ErrorFormat("Job has issuePart {0} that does not match manifestation ID {1}",
                    //    job.IssuePart, MetsManifestation.Id);
                    return false;

                }
            }
            else if (job.VolumePart.HasText())
            {
                // a multiple manifestation volume
                if (job.VolumePart != MetsManifestation.Id)
                {
                    //Log.ErrorFormat("Job has VolumePart {0} that does not match manifestation ID {1}",
                    //    job.VolumePart, MetsManifestation.Id);
                    return false;
                }
            }
            else
            {
                // a single manifestation
                if (job.Identifier != MetsManifestation.Id)
                {
                    //Log.ErrorFormat("Job has Identifier {0} that does not match manifestation ID {1}",
                    //    job.Identifier, MetsManifestation.Id);
                    return false;
                }
            }
            return true;
        }
    }
}
