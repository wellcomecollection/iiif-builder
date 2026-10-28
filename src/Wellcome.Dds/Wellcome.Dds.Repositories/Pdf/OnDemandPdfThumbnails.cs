using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// Bounds what an anonymous /thumb request can make the server do. Generating a thumbnail means
/// fetching a PDF of up to <see cref="PdfThumbnailOptions.MaxPdfBytes"/> and running a rasteriser for
/// up to <see cref="PdfThumbnailOptions.TimeoutSeconds"/>, so, process-wide:
/// <list type="bullet">
/// <item>one generation per identifier at a time: concurrent requests for the same identifier share it;</item>
/// <item>at most <see cref="PdfThumbnailOptions.OnDemandMaxConcurrent"/> generations at a time, others queue;</item>
/// <item>a result that produced no thumbnail (failed, too large, no PDF, not permitted) is remembered for
/// <see cref="PdfThumbnailOptions.FailureMemoryMinutes"/> and returned without retrying.</item>
/// </list>
/// Registered as a singleton; the generation runs in its own DI scope so it survives the request that
/// started it disconnecting while others wait for it.
/// </summary>
public class OnDemandPdfThumbnails
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly PdfThumbnailOptions options;
    private readonly ILogger<OnDemandPdfThumbnails> logger;
    private readonly SemaphoreSlim concurrency;
    private readonly ConcurrentDictionary<string, Task<PdfThumbnailResult>> inFlight = new();
    private readonly ConcurrentDictionary<string, (DateTime Until, PdfThumbnailResult Result)> remembered = new();

    public OnDemandPdfThumbnails(
        IServiceScopeFactory scopeFactory,
        IOptions<PdfThumbnailOptions> options,
        ILogger<OnDemandPdfThumbnails> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options.Value;
        this.logger = logger;
        concurrency = new SemaphoreSlim(Math.Max(1, this.options.OnDemandMaxConcurrent));
    }

    /// <summary>
    /// How many generations are running or queued; for tests and diagnostics.
    /// </summary>
    public int InFlight => inFlight.Count;

    /// <summary>
    /// Generate the thumbnail for <paramref name="ddsId"/> if nothing is stored, with the limits above.
    /// <paramref name="waitToken"/> (the request's) stops this caller waiting; it does not cancel a
    /// generation that other callers may be sharing.
    /// </summary>
    public async Task<PdfThumbnailResult> Ensure(DdsIdentity ddsId, CancellationToken waitToken = default)
    {
        var identifier = ddsId.Value;
        if (remembered.TryGetValue(identifier, out var memo))
        {
            if (memo.Until > DateTime.UtcNow)
            {
                return memo.Result;
            }
            remembered.TryRemove(identifier, out _);
        }

        var task = inFlight.GetOrAdd(identifier, _ => Generate(ddsId));
        try
        {
            return await task.WaitAsync(waitToken);
        }
        finally
        {
            if (task.IsCompleted)
            {
                inFlight.TryRemove(new KeyValuePair<string, Task<PdfThumbnailResult>>(identifier, task));
            }
        }
    }

    private async Task<PdfThumbnailResult> Generate(DdsIdentity ddsId)
    {
        // Yield so GetOrAdd's factory returns the task before any of the work runs on the caller's thread.
        await Task.Yield();
        await concurrency.WaitAsync();
        try
        {
            PdfThumbnailResult result;
            using (var scope = scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IPdfThumbnailService>();
                result = await service.EnsureThumbnail(ddsId, PdfThumbnailRefresh.KeepExisting, CancellationToken.None);
            }
            if (!result.ThumbnailAvailable)
            {
                var until = DateTime.UtcNow.AddMinutes(options.FailureMemoryMinutes);
                remembered[ddsId.Value] = (until, result);
                logger.LogInformation("On-demand PDF thumbnail for {Identifier} produced nothing ({Outcome}); not retrying until {Until:u}",
                    ddsId.Value, result.Outcome, until);
            }
            return result;
        }
        finally
        {
            concurrency.Release();
            inFlight.TryRemove(ddsId.Value, out _);
        }
    }
}
