using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.IO;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    /// <summary>
    /// TODO - we need a new impl of this class with S3-backed sentinels
    /// </summary>
    public class StatusProvider : IStatusProvider
    {
        private DdsOptions ddsOptions;

        public StatusProvider(IOptions<DdsOptions> ddsOptions)
        {
            this.ddsOptions = ddsOptions.Value;
        }

        private const string GoFileText = @"
This file needs to be present and have something in for the DDS to run DLCS ingest and dashboard processes.

It acts as a 'dead man's handle' because if the METS file system is unavailable, this file is unavailable.";

        public bool Stop()
        {
            try
            {
                if (File.Exists(ddsOptions.GoFile))
                {
                    File.Delete(ddsOptions.GoFile);
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
                    File.WriteAllText(ddsOptions.GoFile, GoFileText);
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
                if (!File.Exists(ddsOptions.GoFile))
                {
                    return false;
                }
                if (new FileInfo(ddsOptions.GoFile).Length == 0)
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
                return StringUtils.GetNullableDateTime(ddsOptions.EarliestJobDateTime);
            }
        }

        public DateTime? LatestJobToTake
        {
            get
            {
                if (ddsOptions.MinimumJobAgeMinutes > 0)
                {
                    return DateTime.Now.AddMinutes(0 - ddsOptions.MinimumJobAgeMinutes);
                }
                return null; 
            }

        }

        public DateTime? WriteHeartbeat()
        {
            if (string.IsNullOrWhiteSpace(ddsOptions.StatusProviderHeartbeat))
            {
                return null;
            }
            var now = DateTime.Now;
            var value = now.ToString("F", CultureInfo.CurrentCulture);
            try
            {
                File.WriteAllText(ddsOptions.StatusProviderHeartbeat, value);
            }
            catch
            {
                return null;
            }
            return now;
        }

        public DateTime? GetHeartbeat()
        {
            if (string.IsNullOrWhiteSpace(ddsOptions.StatusProviderHeartbeat))
            {
                return null;
            }
            if (File.Exists(ddsOptions.StatusProviderHeartbeat))
            {
                DateTime dt;
                var value = File.ReadAllText(ddsOptions.StatusProviderHeartbeat).Trim();
                if (DateTime.TryParseExact(value, "F", CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                {
                    return dt;
                }
            }
            return null;
        }

        public bool LogSpecial(string message)
        {
            if (!File.Exists(ddsOptions.StatusProviderLogSpecialFile)) return false;
            try
            {
                File.AppendAllText(ddsOptions.StatusProviderLogSpecialFile, message + Environment.NewLine);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
