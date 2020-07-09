using System.Collections.Generic;
using System.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;

namespace Wellcome.Dds.Dashboard.Models
{
    public class JobsModel
    {
        const string Template = " <span class=\"glyphicon glyphicon-{0}\"></span> ";
        public DlcsIngestJob[] Jobs { get; set; }

        public bool HasProblem(DlcsIngestJob job)
        {
            if (job.DlcsBatches.HasItems() && job.DlcsBatches.Any(b => b.ErrorCode != 0 || b.ErrorText.HasText()))
                return true;

            // other possible problems
            return false;

        }
        public string GetCssClassForJobRow(DlcsIngestJob job)
        {
            if (job.DlcsBatches.HasItems() && job.DlcsBatches.Any(b => b.ErrorCode != 0 || b.ErrorText.HasText()))
                return "danger";
            if (job.StartProcessed == null)
                return "";
            if (job.EndProcessed == null)
                return "info";
            return "";
        }

        public string GetIconForJobRow(DlcsIngestJob job)
        {
            if (job.DlcsBatches.HasItems() && job.DlcsBatches.Any(b => b.ErrorCode != 0 || b.ErrorText.HasText()))
                return string.Format(Template, "exclamation-sign");
            if (job.StartProcessed == null)
                return string.Format(Template, "upload");
            if (job.EndProcessed == null)
                return string.Format(Template, "hourglass");
            return "";
        }

        public string GetCssClassForJobRow(ICollection<DlcsBatch> dlcsBatches)
        {
            return "";
        }

        public string GetIconForJobRow(ICollection<DlcsBatch> dlcsBatches)
        {
            return "";
        }

        public string GetCssClassForJobRow(DlcsIngestJob dlcsBatches, bool isDetail)
        {
            return (isDetail ? "" : "row-detail ") + "";
        }

        public string GetCssClassForJobRow(bool isDetail)
        {
            return isDetail ? "" : "row-detail";
        }
    }
}