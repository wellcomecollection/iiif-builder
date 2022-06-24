using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Utils.Threading;
using Xunit;

namespace Utils.Tests.Threading
{
    public class TaskExTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TimeoutAfter_ReturnsTaskResult_IfNotTimeout(bool throwOnTimeout)
        {
            // Arrange
            const string expected = "foo";
            Task<string> task = new TaskFactory().StartNew(() => expected);

            // Act
            var result = await task.TimeoutAfter(1000, throwOnTimeout);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task TimeoutAfter_ReturnsDefaultT_IfTimesOutAndNotThrowOnTimeout()
        {
            // Arrange
            const int timeout = 100;
            Task<string> task = new TaskFactory().StartNew(() =>
            {
                Thread.Sleep(timeout * timeout);
                return "foo";
            });

            // Act
            var result = await task.TimeoutAfter(timeout);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task TimeoutAfter_ThrowsTimeoutException_IfTimesOutAndThrowOnTimeout()
        {
            // Arrange
            const int timeout = 100;
            Task<string> task = new TaskFactory().StartNew(() =>
            {
                Thread.Sleep(timeout * timeout);
                return "foo";
            });

            // Act
            Func<Task> action = async () => await task.TimeoutAfter(timeout, true);

            // Assert
            await action.Should().ThrowAsync<TimeoutException>();
        }
    }
}