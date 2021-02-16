using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShellProgressBar;
using Utils;
using Wellcome.Dds.Catalogue;

namespace CatalogueClient.ToolSupport
{
    public static class DumpUtils
    {
        public static async Task DownloadDump()
        {
            const int bufferSize = 81920;
            using var client = new HttpClient();
            using var response = await client.GetAsync(
                Settings.CatalogueDump, HttpCompletionOption.ResponseHeadersRead);
            Debug.Assert(response.Content.Headers.ContentLength != null, "response.Content.Headers.ContentLength != null");
            var contentLength = response.Content.Headers.ContentLength.Value;
            int totalTicks = AsMb(contentLength);
            await using Stream download = await response.Content.ReadAsStreamAsync();
            using var progressBar = new ProgressBar(totalTicks, $"Dump file is {totalTicks} MB");
            var progressWrapper = new Progress<long>(totalBytes => Report(progressBar, totalTicks, totalBytes, contentLength));
            await using Stream destination = File.Open(Settings.LocalDumpPath, FileMode.Create);
            await download.CopyToAsync(destination, bufferSize, progressWrapper, CancellationToken.None);
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
        
        
        public static void UnpackDump()
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
        
        public static IEnumerable<string> ReadLines(DumpLoopInfo info)
        {
            var lines = File.ReadLines(Settings.LocalExpandedPath);
            if (info.Filter.HasText())
            {
                foreach (var line in lines)
                {
                    info.TotalCount++;
                    if (line.Contains(info.Filter, StringComparison.Ordinal))
                    {
                        info.MatchCount++;
                        yield return line;
                    }
                }
            }
            else
            {
                foreach (var line in lines)
                {
                    info.TotalCount++;
                    info.MatchCount++;
                    yield return line;
                }
            }
        }
        
        public static JsonSerializerOptions GetSerialiserOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreNullValues = true,
                PropertyNameCaseInsensitive = true
            };
            return options;
        }
        
        public static void FindDigitisedBNumbers(DumpLoopInfo info, ICatalogue catalogue)
        {
            var options = GetSerialiserOptions();
            Console.WriteLine($"dump line| message    | work id  | digitised b numbers            | Sierra system b numbers");
            foreach (var line in ReadLines(info))
            {
                if (info.MatchCount % info.Skip == 0)
                {
                    info.UsedLines++;
                    var work = catalogue.FromDumpLine(line, options);
                    var sierraSystemBNumbers = work.GetSierraSystemBNumbers();
                    var digitalLocationBNumbers = work.GetDigitisedBNumbers();
                    foreach (var digitalLocationBNumber in digitalLocationBNumbers)
                    {
                        var added = info.UniqueDigitisedBNumbers.Add(digitalLocationBNumber);
                        if (!added)
                        {
                            info.BNumbersInMoreThanOneLine.Add(digitalLocationBNumber);
                        }
                    }

                    var count = info.TotalCount.ToString().PadLeft(8);
                    var flagCol = "";
                    if (!digitalLocationBNumbers.Any())
                    {
                        flagCol = "NO-DIG-B";
                    }
                    else
                    {
                        var intersect = digitalLocationBNumbers.Intersect(sierraSystemBNumbers);
                        if (!intersect.Any())
                        {
                            flagCol = "MISMATCH";
                        }
                    }

                    flagCol = flagCol.PadRight(10);
                    var digBNums = String.Join(',', digitalLocationBNumbers).PadRight(30);
                    var workBNums = String.Join(',', sierraSystemBNumbers);
                    Console.WriteLine($"{count} | {flagCol} | {work.Id} | {digBNums} | {workBNums}");
                }
            }
            Console.WriteLine("--------------------------------------------------------------------------------------------");
            Console.WriteLine($"{info.UsedLines} total work lines read from dump, from which ");
            Console.WriteLine($"{info.UniqueDigitisedBNumbers.Count} digitised b numbers were extracted, of which ");
            Console.WriteLine($"{info.BNumbersInMoreThanOneLine.Count} appeared in more than one line.");
        }
    }
}