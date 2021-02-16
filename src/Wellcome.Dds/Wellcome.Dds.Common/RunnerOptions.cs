using System;

namespace Wellcome.Dds.Common
{
    /// <summary>
    /// A collection of options for controlling WorkflowRunner processing.
    /// </summary>
    public class RunnerOptions
    {
        /// <summary>
        /// Create DLCS Job in database to be processed by DlcsJobProcessor.
        /// </summary>
        public bool RegisterImages { get; set; }
        
        /// <summary>
        /// Save metadata from Catalogue API to db
        /// </summary>
        public bool RefreshFlatManifestations { get; set; } 
        
        /// <summary>
        /// Create IIIF v3 and save to S3.
        /// </summary>
        public bool RebuildIIIF { get; set; }
        
        public bool RebuildTextCaches { get; set; } 
        
        /// <summary>
        /// Create W3C annotation and save to S3.
        /// </summary>
        public bool RebuildAllAnnoPageCaches { get; set; }

        public RunnerOptionsFlags ToFlags()
        {
            var flags = RunnerOptionsFlags.None;
            if (RegisterImages) flags = flags | RunnerOptionsFlags.RegisterImages;
            if (RefreshFlatManifestations) flags = flags | RunnerOptionsFlags.RefreshFlatManifestations;
            if (RebuildIIIF) flags = flags | RunnerOptionsFlags.RebuildIIIF;
            if (RebuildTextCaches) flags = flags | RunnerOptionsFlags.RebuildTextCaches;
            if (RebuildAllAnnoPageCaches) flags = flags | RunnerOptionsFlags.RebuildAllAnnoPageCaches;
            return flags;
        }

        public static RunnerOptions FromFlags(RunnerOptionsFlags flags)
        {
            return new()
            {
                RegisterImages            = (flags & RunnerOptionsFlags.RegisterImages) == RunnerOptionsFlags.RegisterImages,
                RefreshFlatManifestations = (flags & RunnerOptionsFlags.RefreshFlatManifestations) == RunnerOptionsFlags.RefreshFlatManifestations,
                RebuildIIIF              = (flags & RunnerOptionsFlags.RebuildIIIF) == RunnerOptionsFlags.RebuildIIIF,
                RebuildTextCaches         = (flags & RunnerOptionsFlags.RebuildTextCaches) == RunnerOptionsFlags.RebuildTextCaches,
                RebuildAllAnnoPageCaches  = (flags & RunnerOptionsFlags.RebuildAllAnnoPageCaches) == RunnerOptionsFlags.RebuildAllAnnoPageCaches
            };
        }

        public int ToInt32()
        {
            return (int)ToFlags();
        }

        public static RunnerOptions FromInt32(int flagsInt)
        {
            return FromFlags((RunnerOptionsFlags) flagsInt);
        }

        public bool HasWorkToDo()
        {
            return RegisterImages || RefreshFlatManifestations || RebuildIIIF || RebuildTextCaches ||
                   RebuildAllAnnoPageCaches;
        }

        public string ToDisplayString()
        {
            return ToFlags().ToString();
        }

        public static RunnerOptions AllButDlcsSync()
        {
            return new()
            {
                RegisterImages = false,
                RefreshFlatManifestations = true,
                RebuildIIIF = true,
                RebuildTextCaches = true,
                RebuildAllAnnoPageCaches = true
            };
        }
    }

    [Flags]
    public enum RunnerOptionsFlags
    {
        None = 0,
        RegisterImages = 1,
        RefreshFlatManifestations = 2,
        RebuildIIIF = 4,
        RebuildTextCaches = 8,
        RebuildAllAnnoPageCaches = 16
    }
}