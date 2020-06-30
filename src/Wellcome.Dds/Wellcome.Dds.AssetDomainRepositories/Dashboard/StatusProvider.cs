using System;
using System.Globalization;
using System.IO;
using Digirati.Util;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public class StatusProvider : IStatusProvider
    {
        private static readonly string DdsGoFile = StringUtils.GetAppSetting("DDS-GO-FILE", null);
        private static readonly string HeartbeatFile = StringUtils.GetAppSetting("StatusProvider-Heartbeat", null);
        private static readonly string LogSpecialFile = StringUtils.GetAppSetting("StatusProvider-LogSpecialFile", null);
        private static readonly DateTime? StartCutOff = StringUtils.GetNullableDateFromAppSetting("Dashboard-Earliest-Job-DateTime");
        private static readonly int MinimumJobAgeMinutes = StringUtils.GetInt32FromAppSetting("Dashboard-Minimum-Job-Age-Minutes", 0);

        private const string GoFileText = @"
This file needs to be present and have something in for the DDS to run DLCS ingest and dashboard processes.

It acts as a 'dead man's handle' because if the METS file system is unavailable, this file is unavailable.";

        public bool Stop()
        {
            try
            {
                if (File.Exists(DdsGoFile))
                {
                    File.Delete(DdsGoFile);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Start()
        {
            try
            {
                if (Stop())
                {
                    File.WriteAllText(DdsGoFile, GoFileText);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        public bool RunProcesses
        {
            get
            {
                if (!File.Exists(DdsGoFile))
                {
                    return false;
                }
                if (new FileInfo(DdsGoFile).Length == 0)
                {
                    return false;
                }
                return true;
            }
        }

        public DateTime? EarliestJobToTake
        {
            get
            {
                return StartCutOff;
            }
        }

        public DateTime? LatestJobToTake
        {
            get
            {
                if (MinimumJobAgeMinutes > 0)
                {
                    return DateTime.Now.AddMinutes(0 - MinimumJobAgeMinutes);
                }
                return null; 
            }

        }

        public DateTime? WriteHeartbeat()
        {
            if (string.IsNullOrWhiteSpace(HeartbeatFile))
            {
                return null;
            }
            var now = DateTime.Now;
            var value = now.ToString("F", CultureInfo.CurrentCulture);
            try
            {
                File.WriteAllText(HeartbeatFile, value);
            }
            catch
            {
                return null;
            }
            return now;
        }

        public DateTime? GetHeartbeat()
        {
            if (string.IsNullOrWhiteSpace(HeartbeatFile))
            {
                return null;
            }
            if (File.Exists(HeartbeatFile))
            {
                DateTime dt;
                var value = File.ReadAllText(HeartbeatFile).Trim();
                if (DateTime.TryParseExact(value, "F", CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                {
                    return dt;
                }
            }
            return null;
        }

        public bool LogSpecial(string message)
        {
            if (!File.Exists(LogSpecialFile)) return false;
            try
            {
                File.AppendAllText(LogSpecialFile, message + Environment.NewLine);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
