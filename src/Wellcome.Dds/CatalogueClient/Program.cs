﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using CatalogueClient.ToolSupport;
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
            string? id = null,
            FileInfo? file = null,
            bool update = false,
            string? bulkop = null,
            int skip = 1)
        {
            var sw = new Stopwatch();
            sw.Start();
            var dumpUtils = new DumpUtils();
            if (update)
            {
                await dumpUtils.DownloadDump();
                dumpUtils.UnpackDump();
            }
            
            if (id != null)
            {
                ShowSingleWork(id);
            }

            if (file != null && file.Exists)
            {
                ShowManyWorksFromFile(file);
            }

            if (bulkop.HasText())
            {
                var fi = new FileInfo(Settings.LocalExpandedPath);
                Console.WriteLine(
                    $"Using decompressed dump file dated {fi.LastWriteTime}, {StringUtils.FormatFileSize(fi.Length)}");
                Console.WriteLine("(Update the catalogue dump with --update)");
            }

            var dumpLoopInfo = new DumpLoopInfo
            {
                Skip = skip, 
                Filter = DumpLoopInfo.IIIFLocationFilter
            };
            switch (bulkop)
            {
                case "count-digitised-locations":
                    foreach (var line in dumpUtils.ReadLines(dumpLoopInfo))
                    {
                        if (dumpLoopInfo.TotalCount % 1000 == 0)
                        {
                            Console.Write($"\rIIIF Presentation in {dumpLoopInfo.MatchCount}/{dumpLoopInfo.TotalCount} works.");
                        }
                    }
                    Console.Write($"\rIIIF Presentation in {dumpLoopInfo.MatchCount}/{dumpLoopInfo.TotalCount} works.");
                    break;
                case "display-bnumber":
                    dumpUtils.FindDigitisedBNumbers(dumpLoopInfo, GetCatalogue());
                    break;
                case "compare-availabilities":
                    CompareAvailabilities(dumpUtils, skip);
                    break;
                default:
                    Console.WriteLine("(No bulk operation specified)");
                    break;
            }
            Console.WriteLine();
            sw.Stop();  
            Console.WriteLine($"Finished in {sw.Elapsed.TotalSeconds} seconds.");
        }

        private static void CompareAvailabilities(DumpUtils dumpUtils, int skip)
        {
            var catalogue = GetCatalogue();
            var options = DumpUtils.GetSerialiserOptions();
            int counter = 0;
            foreach (var line in dumpUtils.ReadLines(new DumpLoopInfo {Skip = skip}))
            {
                if (counter++ % 100 == 0)
                {
                    Console.WriteLine("processing: " + counter);
                }
                var work = catalogue.FromDumpLine(line, options);
                if(work == null) continue;
                
                bool? online = work.IsOnline();
                if (online == null) continue; // availabilities is NOT present
                
                if (work.HasIIIFDigitalLocation() != online.Value)
                {
                    Console.WriteLine("HasDigitalLocation disagrees with IsOnline for " + work.Id);
                }
            }
        }


        private static void ShowManyWorksFromFile(FileInfo file)
        {
            var catalogue = GetCatalogue();
            var options = DumpUtils.GetSerialiserOptions();
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
            var options = DumpUtils.GetSerialiserOptions();
            var work = catalogue.GetWorkByOtherIdentifier(id).Result;
            Console.Write(JsonSerializer.Serialize(work, options));
            Console.WriteLine();
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


    }
}