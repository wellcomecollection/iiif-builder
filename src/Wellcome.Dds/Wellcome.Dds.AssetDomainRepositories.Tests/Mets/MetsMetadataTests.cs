using System.Collections.Generic;
using FluentAssertions;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class MetsMetadataTests
    {
        [Theory]
        [InlineData("22mn 49s", 1369)]
        [InlineData("1mn 41s", 101)]
        [InlineData("9mn 46s", 586)]
        public void TestDurationParsing(string input, int expected)
        {
            PremisMetadata.ParseDuration(input).Should().Be(expected);
        }
    }
}