using System;
using System.ComponentModel.DataAnnotations;

namespace Wellcome.Dds.AssetDomain.Workflow
{
    public class WorkflowJob
    {
        [Key]
        public string Identifier { get; set; }
        public bool ForceTextRebuild { get; set; }
        public bool Waiting { get; set; }
        public bool Finished { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Taken { get; set; }
        public int FirstDlcsJobId { get; set; }
        public int DlcsJobCount { get; set; }
        public int ExpectedTexts { get; set; }
        public int TextsAlreadyOnDisk { get; set; }
        public int TextsBuilt { get; set; }
        public int AnnosAlreadyOnDisk { get; set; }
        public int AnnosBuilt { get; set; }
        public long PackageBuildTime { get; set; }
        public long TextAndAnnoBuildTime { get; set; }
        public long TotalTime { get; set; }
        public string Error { get; set; }
        public int Words { get; set; }
        public int TextPages { get; set; }
        public int TimeSpentOnTextPages { get; set; }
        public int? WorkflowOptions { get; set; }

        public string GetAltoRate()
        {
            if (TextPages <= 0 || TimeSpentOnTextPages <= 0)
            {
                return "n/a";
            }
            return ((1000 * TextPages) / TimeSpentOnTextPages).ToString();
        }

        public override string ToString()
        {
            return $"{Identifier}: {Created}, options: {WorkflowOptions}";
        }
    }
}
