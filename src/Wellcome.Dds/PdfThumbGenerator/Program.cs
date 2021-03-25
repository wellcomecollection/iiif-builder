using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Ghostscript.NET.Rasterizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OAuth2;
using ShellProgressBar;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;

namespace PdfThumbGenerator
{
    class Program
    {
        static async Task Main(string file = @"C:\temp\Digitised_PDF_list.csv")
        {
            await Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services
                        .AddMemoryCache()
                        .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
                        .AddSingleton<IMetsRepository, MetsRepository>()
                        .AddSingleton<StorageServiceClient>()
                        .AddSingleton<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                        .AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>))
                        .AddHostedService<PdfGenerator>(provider =>
                        {
                            var metsRepo = provider.GetService<IMetsRepository>();
                            return new PdfGenerator(metsRepo, file);
                        });

                    services.AddHttpClient<OAuth2ApiConsumer>();
                    
                    var configuration = context.Configuration;
                    var awsOptions = configuration.GetAWSOptions("Dds-AWS");
                    services.AddDefaultAWSOptions(awsOptions);
                    
                    services.Configure<StorageOptions>(configuration.GetSection("Storage"));
                    services.Configure<BinaryObjectCacheOptionsByType>(configuration.GetSection("BinaryObjectCache"));
                    
                    var factory = services.AddNamedS3Clients(configuration, NamedClient.All);
                    services.AddSingleton<IStorage, S3Storage>(opts =>
                        ActivatorUtilities.CreateInstance<S3Storage>(opts,
                            factory.Get(NamedClient.Dds)));
                })
                .RunConsoleAsync();
        }
    }
    
    public class PdfGenerator : IHostedService
    {
        private readonly IMetsRepository metsRepository;
        private readonly string file;

        public PdfGenerator(IMetsRepository metsRepository, string file)
        {
            this.metsRepository = metsRepository;
            this.file = file;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // read csv
            var bornDigitalPdfs = GetBornDigitalPdfs(file);

            using var progressBar = new ProgressBar(bornDigitalPdfs.Count, $"Processing {bornDigitalPdfs.Count} PDFs");
            foreach (var pdf in bornDigitalPdfs)
            {
                // find PDF
                progressBar.Tick();
            }
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        
        private static List<BornDigitalPdf> GetBornDigitalPdfs(string file)
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<BornDigitalPdf>();
            return records.ToList();
        }
    }

    public class BornDigitalPdf
    {
        public string Identifier { get; set; }
        public string Processed { get; set; }
        public string Title { get; set; }
    }
}