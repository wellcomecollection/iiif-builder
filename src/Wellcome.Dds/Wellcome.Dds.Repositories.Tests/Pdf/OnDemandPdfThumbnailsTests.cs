using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Pdf;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Pdf;

public class OnDemandPdfThumbnailsTests
{
    private readonly IPdfThumbnailService service = A.Fake<IPdfThumbnailService>();
    private readonly PdfThumbnailOptions options = new() { OnDemandMaxConcurrent = 2, FailureMemoryMinutes = 10 };
    private readonly DdsIdentity ddsId = ParsingIdentityService.Parse("ARTCOO/B/15");

    private OnDemandPdfThumbnails Sut()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => service);
        var provider = services.BuildServiceProvider();
        return new OnDemandPdfThumbnails(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options),
            NullLogger<OnDemandPdfThumbnails>.Instance);
    }

    private static PdfThumbnailResult Result(PdfThumbnailOutcome outcome) => new("ARTCOO/B/15", "_pdf_thumbs/ARTCOO/B/15.jpg", outcome);

    [Fact]
    public async Task Concurrent_Requests_For_One_Identifier_Share_A_Single_Generation()
    {
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource<PdfThumbnailResult>();
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .ReturnsLazily(() => { started.TrySetResult(); return finish.Task; });
        var sut = Sut();

        var first = sut.Ensure(ddsId);
        var second = sut.Ensure(ddsId);
        await started.Task;
        finish.SetResult(Result(PdfThumbnailOutcome.Generated));
        var results = await Task.WhenAll(first, second);

        results.Should().AllSatisfy(r => r.Outcome.Should().Be(PdfThumbnailOutcome.Generated));
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, PdfThumbnailRefresh.KeepExisting, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        sut.InFlight.Should().Be(0);
    }

    [Theory]
    [InlineData(PdfThumbnailOutcome.Failed)]
    [InlineData(PdfThumbnailOutcome.TooLarge)]
    [InlineData(PdfThumbnailOutcome.NoPdf)]
    [InlineData(PdfThumbnailOutcome.NotPermitted)]
    public async Task A_Result_Without_A_Thumbnail_Is_Remembered_And_Not_Retried(PdfThumbnailOutcome outcome)
    {
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .Returns(Result(outcome));
        var sut = Sut();

        var first = await sut.Ensure(ddsId);
        var second = await sut.Ensure(ddsId);

        first.Outcome.Should().Be(outcome);
        second.Should().BeSameAs(first);
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Generated_Thumbnail_Is_Not_Remembered_So_The_Next_Request_Finds_It_In_Storage()
    {
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .Returns(Result(PdfThumbnailOutcome.Generated));
        var sut = Sut();

        await sut.Ensure(ddsId);
        await sut.Ensure(ddsId);

        // the second call goes to the service, which answers AlreadyExists from S3 in real life
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task No_More_Than_The_Configured_Number_Of_Generations_Run_At_Once()
    {
        var running = 0;
        var maxObserved = 0;
        var release = new TaskCompletionSource<PdfThumbnailResult>();
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                var now = Interlocked.Increment(ref running);
                InterlockedMax(ref maxObserved, now);
                var result = await release.Task;
                Interlocked.Decrement(ref running);
                return result;
            });
        var sut = Sut();

        var requests = Enumerable.Range(0, 5).Select(i => sut.Ensure(ParsingIdentityService.Parse($"ARTCOO/B/{i}"))).ToList();
        // let the first two start and the rest queue on the semaphore
        await WaitUntil(() => Volatile.Read(ref running) == 2);
        await Task.Delay(100);
        Volatile.Read(ref running).Should().Be(2);
        release.SetResult(Result(PdfThumbnailOutcome.Generated));
        await Task.WhenAll(requests);

        maxObserved.Should().Be(2);
        sut.InFlight.Should().Be(0);
    }

    [Fact]
    public async Task A_Waiting_Request_Can_Give_Up_Without_Cancelling_The_Shared_Generation()
    {
        var finish = new TaskCompletionSource<PdfThumbnailResult>();
        A.CallTo(() => service.EnsureThumbnail(A<DdsIdentity>._, A<PdfThumbnailRefresh>._, A<CancellationToken>._))
            .ReturnsLazily(() => finish.Task);
        var sut = Sut();
        using var impatient = new CancellationTokenSource();

        var patient = sut.Ensure(ddsId);
        var gaveUp = sut.Ensure(ddsId, impatient.Token);
        impatient.Cancel();

        await ((Func<Task>) (() => gaveUp)).Should().ThrowAsync<OperationCanceledException>();
        finish.SetResult(Result(PdfThumbnailOutcome.Generated));
        (await patient).Outcome.Should().Be(PdfThumbnailOutcome.Generated);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int current;
        while ((current = Volatile.Read(ref location)) < value && Interlocked.CompareExchange(ref location, value, current) != current)
        {
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("condition not met");
            }
            await Task.Delay(10);
        }
    }
}
