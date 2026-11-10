using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wellcome.Dds.Repositories.Pdf;

public static class PdfThumbnailServiceCollectionExtensions
{
    /// <summary>
    /// Register PDF thumbnail generation: options from the "PdfThumbnails" section, a rasteriser chosen
    /// once at startup, and the service. Requires IMetsRepository, IIdentityService and the named S3
    /// clients to be registered too.
    /// </summary>
    /// <param name="lifetime">Scoped in the web apps (IMetsRepository is); Singleton in the CLI tool.</param>
    public static IServiceCollection AddPdfThumbnails(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        services.Configure<PdfThumbnailOptions>(configuration.GetSection(PdfThumbnailOptions.SectionName));
        services.AddSingleton<IPdfRasteriser>(provider => PdfRasteriserFactory.Create(
            provider.GetRequiredService<IOptions<PdfThumbnailOptions>>().Value,
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("PdfRasteriser")));
        services.Add(new ServiceDescriptor(typeof(IPdfThumbnailService), typeof(PdfThumbnailService), lifetime));
        // process-wide limits for on-demand (/thumb) generation; creates its own scope for the service
        services.AddSingleton<OnDemandPdfThumbnails>();
        return services;
    }
}
