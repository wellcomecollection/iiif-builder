using System.Collections.Generic;
using FluentAssertions;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.IIIFBuilding;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class MetsMetadataTests
    {
        [Theory]
        [InlineData("22mn 49s", 1369)]
        [InlineData("1mn 41s", 101)]
        [InlineData("9mn 46s", 586)]
        [InlineData("58s", 58)]
        [InlineData("580s", 580)]
        [InlineData("99", 99)]
        [InlineData("20", 20)] // 20
        [InlineData("2:20", 140)] // 140s
        [InlineData("1:2:20", 3600 + 2*60 + 20)] // 3740s
        [InlineData("1:59:20", 3600 + 59*60 + 20)] 
        public void TestDurationParsing(string input, int expected)
        {
            PremisMetadata.ParseDuration(input).Should().Be(expected);
        }
    }
}