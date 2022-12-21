using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ShellProgressBar;
using Utils;
using Wellcome.Dds.Catalogue;

namespace CatalogueClient.ToolSupport
{
    public class DumpUtils
    {
        private readonly string dumpPath;

        public DumpUtils(string dumpPath)
        {
            this.dumpPath = dumpPath;
            if (!File.Exists(dumpPath))
            {
                throw new InvalidOperationException($"Specified dump file {dumpPath} does not exist!");
            }
        }

        public DumpUtils()
        {
            dumpPath = Settings.LocalExpandedPath;
        }
        
        public async Task DownloadDump()
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
        
        
        public void UnpackDump()
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
        
        public IEnumerable<string> ReadLines(DumpLoopInfo info)
        {
            var lines = File.ReadLines(dumpPath);
            if (info.Filter.HasText())
            {
                foreach (var line in lines)
                {
                    info.TotalCount++;
                    if (line.Contains(info.Filter, StringComparison.Ordinal))
                    {
                        info.MatchCount++;
                        if (info.MatchCount > info.Offset)
                        {
                            yield return line;
                        }
                    }
                }
            }
            else
            {
                foreach (var line in lines)
                {
                    info.TotalCount++;
                    info.MatchCount++;
                    if (info.MatchCount > info.Offset)
                    {
                        yield return line;
                    }
                }
            }
        }
        
        public static JsonSerializerOptions GetSerialiserOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return options;
        }
        
        public void FindDigitisedBNumbers(DumpLoopInfo info, ICatalogue catalogue)
        {
            var options = GetSerialiserOptions();
            Console.WriteLine($"dump line| message    | work id  | digitised b numbers            | Sierra system b numbers");
            foreach (var line in ReadLines(info))
            {
                if (info.MatchCount % info.Skip == 0)
                {
                    info.UsedLines++;
                    var work = catalogue.FromDumpLine(line, options);
                    if(work == null) continue;
                    
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