using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;
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

        public bool Stop()
        {
            var req = new DeleteObjectRequest
            {
                BucketName = ddsOptions.StatusContainer,
                Key = ddsOptions.GoFile
            };
            try
            {                
                amazonS3.DeleteObjectAsync(req);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Start()
        {
            var req = MakePutTextRequest(ddsOptions.GoFile, GoFileText);
            try
            {                
                var res = amazonS3.PutObjectAsync(req).Result;
                return true;         
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool RunProcesses
        {
            get
            {
                var req = MakeGetObjectRequest(ddsOptions.GoFile);
                try
                {
                    var res = amazonS3.GetObjectAsync(req).Result;
                    if (res.HttpStatusCode == System.Net.HttpStatusCode.OK && res.ContentLength > 0)
                    {
                        return true;
                    }
                }
                catch(Exception)
                {

                }
                return false;
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
            var req = MakePutTextRequest(ddsOptions.StatusProviderHeartbeat, value);
            try
            {
                var res = amazonS3.PutObjectAsync(req).Result;
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
            var req = MakeGetObjectRequest(ddsOptions.StatusProviderHeartbeat);
            try
            {
                var res = amazonS3.GetObjectAsync(req).Result;
                using(var stream = res.ResponseStream)
                {
                    string s = new StreamReader(stream).ReadToEnd(); 
                    DateTime dt;
                    if (DateTime.TryParseExact(s.Trim(), "F", CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                    {
                        return dt;
                    }
                }
            }
            catch(Exception)
            {

            }
            return null;
        }

        public bool LogSpecial(string message)
        {
            string key = ddsOptions.StatusProviderLogSpecialFile + DateTime.Now.ToString("s");
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
