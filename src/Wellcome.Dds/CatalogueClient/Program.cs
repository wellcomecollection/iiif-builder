using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShellProgressBar;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Catalogue;

namespace CatalogueClient
{
    class Program
    {
        static async Task Main(
            string id = null,
            FileInfo file = null,
            bool update = false,
            string bulkop = null)
        {
            if (update)
            {
                await DownloadDump();
                UnpackDump();
            }
            
            if (id != null)
            {
                ShowSingleWork(id);
            }

            if (file != null && file.Exists)
            {
                ShowManyWorksFromFile(file);
            }

            var sw = new Stopwatch();
            sw.Start();
            var fi = new FileInfo(Settings.LocalExpandedPath);
            Console.WriteLine($"Using decompressed dump file dated {fi.LastWriteTime}, {StringUtils.FormatFileSize(fi.Length)}");
            Console.WriteLine("(Update the catalogue dump with --update)");
            switch (bulkop)
            {
                case "count-digitised-locations":
                    int count = 0;
                    int iiifCount = 0;
                    foreach (var line in File.ReadLines(Settings.LocalExpandedPath))
                    {
                        count++;
                        if (line.Contains("\"iiif-presentation\"", StringComparison.Ordinal))
                        {
                            iiifCount++;
                        }

                        if (count % 1000 == 0)
                        {
                            Console.Write($"\rIIIF Presentation in {iiifCount}/{count} works.");
                        }
                    }
                    Console.Write($"\rIIIF Presentation in {iiifCount}/{count} works.");
                    Console.WriteLine();
                    break;
                default:
                    Console.WriteLine("(No bulk operation specified)");
                    break;
            }
            sw.Stop();
            Console.WriteLine($"Finished in {sw.Elapsed.Seconds} seconds.");
        }
        

        private static void ShowManyWorksFromFile(FileInfo file)
        {
            var catalogue = GetCatalogue();
            var options = GetSerialiserOptions();
            var lines = File.ReadAllLines(file.FullName);
            int counter = 1;
            foreach (var line in lines)
            {
                var identifier = line.Trim();
                if (identifier.IsNullOrEmpty())
                {
                    continue;
                }

                var work = catalogue.GetWorkByOtherIdentifier(identifier).Result;
                Console.WriteLine();
                Console.WriteLine($"~~~~ start {counter} ~~~~~~~~~~~~~~~~~~~~~~~~~");
                Console.Write(JsonSerializer.Serialize(work, options));
                Console.WriteLine();
                Console.WriteLine($"~~~~ end {counter++} ~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                Console.WriteLine();
            }
        }

        private static void ShowSingleWork(string id)
        {
            var catalogue = GetCatalogue();
            var options = GetSerialiserOptions();
            var work = catalogue.GetWorkByOtherIdentifier(id).Result;
            Console.Write(JsonSerializer.Serialize(work, options));
            Console.WriteLine();
        }


        private static async Task DownloadDump()
        {
            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(
                Settings.CatalogueDump, HttpCompletionOption.ResponseHeadersRead);
            var contentLength = response.Content.Headers.ContentLength.Value;
            int totalTicks = AsMb(contentLength);
            await using Stream download = await response.Content.ReadAsStreamAsync();
            using var progressBar = new ProgressBar(totalTicks, $"Dump file is {totalTicks} MB");
            var progressWrapper = new Progress<long>(totalBytes => Report(progressBar, totalTicks, totalBytes, contentLength));
            await using Stream destination = File.Open(Settings.LocalDumpPath, FileMode.Create);
            await download.CopyToAsync(destination, 81920, progressWrapper, CancellationToken.None);
        }

        private static void Report(ProgressBar progressBar, int ticks, long totalBytes, long? contentLength)
        {
            var expectedTick = AsMb(totalBytes);
            while (expectedTick > progressBar.CurrentTick)
            {
                progressBar.Tick();
                progressBar.Message = $"  {progressBar.CurrentTick}/{progressBar.MaxTicks} MB downloaded...";
            }
        }

        private static int AsMb(long bytes)
        {
            return (int) (bytes / (1024 * 1024));
        }
        
        private static void UnpackDump()
        {
            var gz = new FileInfo(Settings.LocalDumpPath);
            using var originalFileStream = gz.OpenRead();
            using var decompressedFileStream = File.Create(Settings.LocalExpandedPath);
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            {
                Console.WriteLine("Decompressing dump file...");
                decompressionStream.CopyTo(decompressedFileStream);
            }

            var sizeDisplay = StringUtils.FormatFileSize(decompressedFileStream.Length);
            Console.WriteLine($"Decompressed dump file is {sizeDisplay} GB.");
        }

        private static ICatalogue GetCatalogue()
        {
            var httpClient = new HttpClient();
            var ddsOptions = new DdsOptions
            {
                ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
            };
            var options = Options.Create(ddsOptions);
            var catalogue = new WellcomeCollectionCatalogue(options, httpClient, new NullLogger<WellcomeCollectionCatalogue>());
            return catalogue;
        }

        private static JsonSerializerOptions GetSerialiserOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreNullValues = true
            };
            return options;
        }
    }
}