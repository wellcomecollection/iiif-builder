using FluentAssertions;
using Wellcome.Dds.Common;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowProcessor.Tests
{
    public class RunnerOptionsTests
    {
        private readonly ITestOutputHelper output;

        public RunnerOptionsTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RunnerOptions_AllButDlcs_Produces_Correct_Flags()
        {
            var allButDlcs = RunnerOptions.AllButDlcsSync();
            
            output.WriteLine(allButDlcs.ToString());
            output.WriteLine(allButDlcs.ToInt32().ToString());
            allButDlcs.RegisterImages.Should().BeFalse();
            allButDlcs.RefreshFlatManifestations.Should().BeTrue();
            allButDlcs.RebuildIIIF.Should().BeTrue();
            allButDlcs.RebuildTextCaches.Should().BeTrue();
            allButDlcs.RebuildAllAnnoPageCaches.Should().BeTrue();
        }

        [Fact]
        public void RunnerOptions_Can_Round_Trip_From_Flags()
        {
            // Arrange
            var allOptions = new RunnerOptions[32];
            var allFlags = new RunnerOptionsFlags[32];
            var test1 = new RunnerOptions
            {
                RebuildIIIF = true,
                RebuildAllAnnoPageCaches = true
            };
            var test2 = new RunnerOptions
            {
                RegisterImages = true,
                RebuildAllAnnoPageCaches = true,
                RefreshFlatManifestations = true
            };

            // Act
            // This really just provides some useful debug output
            for (int i = 0; i < 32; i++)
            {
                allFlags[i] = (RunnerOptionsFlags)i;
                allOptions[i] = RunnerOptions.FromFlags(allFlags[i]);
                output.WriteLine(i + ": " + allFlags[i]);
            }

            // Assert
            var test1AsFlags = test1.ToFlags();
            output.WriteLine("test 1 as flags: " + test1AsFlags);
            output.WriteLine("test 1 as int: " + (int)test1AsFlags);
            var fromTest1 = RunnerOptions.FromFlags(test1AsFlags);
            fromTest1.Should().BeEquivalentTo(test1);

            var test2AsInt = test2.ToInt32();
            output.WriteLine("test 2 as int: " + test2AsInt);
            output.WriteLine("test 2 as flags: " + (RunnerOptionsFlags)test2AsInt);
            test2AsInt.Should().Be(19);
            var fromTest2 = RunnerOptions.FromInt32(19);

            fromTest2.Should().BeEquivalentTo(allOptions[19]);

            fromTest2.RegisterImages.Should().BeTrue();
            fromTest2.RefreshFlatManifestations.Should().BeTrue();
            fromTest2.RebuildIIIF.Should().BeFalse();
            fromTest2.RebuildTextCaches.Should().BeFalse();
            fromTest2.RebuildAllAnnoPageCaches.Should().BeTrue();
        }
    }
}
