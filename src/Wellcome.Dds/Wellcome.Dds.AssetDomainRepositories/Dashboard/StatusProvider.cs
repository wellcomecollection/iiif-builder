using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<bool> Stop(CancellationToken cancellationToken = default)
        {
            try
            {
                if (File.Exists(ddsOptions.GoFile))
                {
                    File.Delete(ddsOptions.GoFile);
                }

                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public async Task<bool> Start(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await Stop())
                {
                    await File.WriteAllTextAsync(ddsOptions.GoFile, GoFileText, cancellationToken);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        public Task<bool> ShouldRunProcesses(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(ddsOptions.GoFile))
            {
                return Task.FromResult(false);
            }

            if (new FileInfo(ddsOptions.GoFile).Length == 0)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
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

        public async Task<DateTime?> WriteHeartbeat(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ddsOptions.StatusProviderHeartbeat))
            {
                return null;
            }
            var now = DateTime.Now;
            var value = now.ToString("F", CultureInfo.CurrentCulture);
            try
            {
                await File.WriteAllTextAsync(ddsOptions.StatusProviderHeartbeat, value, cancellationToken);
            }
            catch
            {
                return null;
            }
            return now;
        }

        public Task<DateTime?> GetHeartbeat(CancellationToken cancellationToken = default)
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
                    return Task.FromResult<DateTime?>(dt);
                }
            }

            return Task.FromResult<DateTime?>(null);
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
