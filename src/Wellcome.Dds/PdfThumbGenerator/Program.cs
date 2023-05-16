using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OAuth2;
using ShellProgressBar;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.Common;

namespace PdfThumbGenerator
{
    // ReSharper disable once UnusedType.Global
    class Program
    {
        // ReSharper disable once UnusedMember.Local
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
                        .AddSingleton<PdfThumbnailUtil>()
                        .AddHostedService(provider =>
                            new PdfGenerator(
                                provider.GetService<ILogger<PdfGenerator>>()!,
                                provider.GetService<IMetsRepository>()!,
                                provider.GetService<PdfThumbnailUtil>()!,
                                file));

                    services.AddHttpClient<OAuth2ApiConsumer>();
                    
                    var configuration = context.Configuration;
                    var awsOptions = configuration.GetAWSOptions("Dds-AWS");
                    services.AddDefaultAWSOptions(awsOptions);
                    
                    services.Configure<DdsOptions>(configuration.GetSection("Dds"));
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
        private readonly ILogger<PdfGenerator> logger;
        private readonly IMetsRepository metsRepository;
        private readonly PdfThumbnailUtil pdfThumbnailUtil;
        private readonly string file;


        public PdfGenerator(ILogger<PdfGenerator> logger, IMetsRepository metsRepository,
            PdfThumbnailUtil pdfThumbnailUtil, string file)
        {
            this.logger = logger;
            this.metsRepository = metsRepository;
            this.pdfThumbnailUtil = pdfThumbnailUtil;
            this.file = file;
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // read csv
            var bornDigitalPdfs = GetBornDigitalPdfs(file);

            using var progressBar = new ProgressBar(bornDigitalPdfs.Count, $"Processing {bornDigitalPdfs.Count} PDFs");
            foreach (var pdf in bornDigitalPdfs)
            {
                await ProcessPdf(pdf);
                progressBar.Tick();
            }
        }

 
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static List<BornDigitalPdf> GetBornDigitalPdfs(string file)
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<BornDigitalPdf>();
            return records.ToList();
        }

        private async Task ProcessPdf(BornDigitalPdf pdf)
        {
            // find PDF items
            try
            {
                logger.LogDebug("Processing {Identifier}", pdf.Identifier);
                IMetsResource? resource = await metsRepository.GetAsync(pdf.Identifier!);
                if (resource is ICollection)
                {
                    throw new InvalidOperationException(
                        $"{pdf.Identifier} is a multiple manifestation - update handling");
                }
                var manifestation = resource as IManifestation;
                var pdfItems = manifestation!.Sequence!.Where(s => s.MimeType == "application/pdf");
                
                foreach (var pdfItem in pdfItems)
                {
                    await pdfThumbnailUtil.EnsurePdfThumbnails(
                        () => pdfItem.WorkStore.GetStreamForPathAsync(pdfItem.RelativePath!),
                        pdf.Identifier!);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing pdf {Identifier}", pdf.Identifier);
            }
        }
    }

    public class BornDigitalPdf
    {
        public string? Identifier { get; set; }
        public string? Processed { get; set; }
        public string? Title { get; set; }
    }
}