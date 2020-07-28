using Amazon.S3;
using Amazon.S3.Model;
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
    /// TODO - this class uses S3 blobs as sentinels, as a direct replacement for the filesystem based
    /// StatusProvider class.
    /// There are probably better mechanisms than this.
    /// This is also a very rapid conversion and needs a bit more care and attention.
    /// </summary>
    public class S3StatusProvider : IStatusProvider
    {
        private DdsOptions ddsOptions;
        private IAmazonS3 amazonS3;

        public S3StatusProvider(
            IOptions<DdsOptions> ddsOptions,
            IAmazonS3 amazonS3)
        {
            this.ddsOptions = ddsOptions.Value;
            this.amazonS3 = amazonS3;
        }

        private const string GoFileText = @"
This file needs to be present and have something in for the DDS to run DLCS ingest and dashboard processes.

It acts as a 'dead man's handle' because if the METS file system is unavailable, this file is unavailable.";

        private GetObjectRequest MakeGetObjectRequest(string key)
        {
            return new GetObjectRequest
            {
                BucketName = ddsOptions.StatusContainer,
                Key = key
            };
        }

        private PutObjectRequest MakePutTextRequest(string key, string content)
        {
            return new PutObjectRequest
            {
                BucketName = ddsOptions.StatusContainer,
                Key = key,
                ContentBody = content,
                ContentType = "text/plain"
            };
        }

        public async Task<bool> Stop(CancellationToken cancellationToken = default)
        {
            var req = new DeleteObjectRequest
            {
                BucketName = ddsOptions.StatusContainer,
                Key = ddsOptions.GoFile
            };
            try
            {                
                await amazonS3.DeleteObjectAsync(req, cancellationToken);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> Start(CancellationToken cancellationToken = default)
        {
            var req = MakePutTextRequest(ddsOptions.GoFile, GoFileText);
            try
            {                
                var res = await amazonS3.PutObjectAsync(req, cancellationToken);
                return true;         
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ShouldRunProcesses(CancellationToken cancellationToken = default)
        {
            var req = MakeGetObjectRequest(ddsOptions.GoFile);
            try
            {
                var res = await amazonS3.GetObjectAsync(req, cancellationToken);
                if (res.HttpStatusCode == System.Net.HttpStatusCode.OK && res.ContentLength > 0)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;

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
            var now = DateTime.UtcNow;
            var value = now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            var req = MakePutTextRequest(ddsOptions.StatusProviderHeartbeat, value);
            try
            {
                var res = await amazonS3.PutObjectAsync(req, cancellationToken);
            }
            catch
            {
                return null;
            }
            return now;
        }

        public async Task<DateTime?> GetHeartbeat(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ddsOptions.StatusProviderHeartbeat))
            {
                return null;
            }

            var req = MakeGetObjectRequest(ddsOptions.StatusProviderHeartbeat);
            try
            {
                var res = await amazonS3.GetObjectAsync(req, cancellationToken);
                using (var stream = res.ResponseStream)
                {
                    string s = await new StreamReader(stream).ReadToEndAsync();
                    DateTime dt;
                    if (DateTime.TryParseExact(s.Trim(), "s", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    {
                        return dt;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public bool LogSpecial(string message)
        {
            string key = ddsOptions.StatusProviderLogSpecialFile + DateTime.UtcNow.ToString("s");
            var req = MakePutTextRequest(key, message);
            try
            {
                var res = amazonS3.PutObjectAsync(req).Result;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
