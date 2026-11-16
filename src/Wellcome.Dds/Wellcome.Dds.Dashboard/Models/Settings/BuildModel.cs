using System;

namespace Wellcome.Dds.Dashboard.Models.Settings
{
    public class BuildModel
    {
        public BuildInfo Build { get; set; }
        public string EnvironmentName { get; set; }

        /// <summary>When this process started: the deploy, or the last restart of the container</summary>
        public DateTime Started { get; set; }

        public string Framework { get; set; }
        public string MachineName { get; set; }
    }
}
