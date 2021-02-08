#nullable enable
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Utils.Storage;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{
    public class Helpers
    {
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;

        public Helpers(
            IStorage storage,
            IOptions<DdsOptions> options)
        {
            this.storage = storage;
            ddsOptions = options.Value;
        }

        /// <summary>
        /// We don't have separate S3 buckets with IIIF content that uses localhost paths.
        /// And we don't want to do that, you'd need a lot of test content.
        /// This optionally allows the locally run Dds.Server to rewrite the LinedDataDomain in the response.
        /// This is obviously slower and inefficient, but it only does it if an overriding domain is provided.
        /// It allows localhost to create content in the test S3 bucket, but serve it with correct paths.
        ///
        /// Add an extra local appSetting to the Dds section:
        /// 
        /// "RewriteDomainLinksTo": "http://localhost:8084",
        /// </summary>
        /// <param name="container"></param>
        /// <param name="path"></param>
        /// <param name="contentType"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public async Task<IActionResult> ServeIIIFContent(string container, string path, string contentType,
            ControllerBase controller)
        {            
            var stream = await storage.GetStream(container, path);
            if (stream == null)
            {
                return controller.NotFound($"No IIIF resource found for {path}");
            }
            if (string.IsNullOrWhiteSpace(ddsOptions.RewriteDomainLinksTo))
            {
                // This is the efficient way to return the response
                return controller.File(stream, contentType);
            }

            // This is an inefficient method but allows us to manipulate the response.
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string raw = await reader.ReadToEndAsync();
            string rewritten = raw.Replace(ddsOptions.LinkedDataDomain, ddsOptions.RewriteDomainLinksTo);
            return controller.Content(rewritten, contentType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="container"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<object?> LoadAsJson(string container, string path)
        {
            var stream = await storage.GetStream(container, path);
            if(stream == null)
            {
                return null;
            }
            using StreamReader reader = new(stream);
            using JsonTextReader jsonReader = new(reader);
            return new JsonSerializer().Deserialize(jsonReader);
        }
        
    }
}