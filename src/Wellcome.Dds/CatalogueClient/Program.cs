using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Catalogue;

namespace CatalogueClient
{
    class Program
    {
        static void Main(string id = null, FileInfo file = null)
        {
            if (id != null)
            {
                var catalogue = GetCatalogue();
                var options = GetSerialiserOptions();
                var work = catalogue.GetWorkByOtherIdentifier(id).Result;
                Console.Write(JsonSerializer.Serialize(work, options));
                Console.WriteLine();
            }

            if (file != null && file.Exists)
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