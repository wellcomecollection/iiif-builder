using System.Collections.Generic;
using FluentAssertions;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class MetsMetadataTests
    {
        [Fact]
        public void TestDurationParsing()
        {
            Dictionary<string,double> tests = new Dictionary<string, double>()
            {
                ["22mn 49s"] = 1369,
                ["1mn 41s"] = 101,
                ["9mn 46s"] = 586
            };
            foreach (var test in tests)
            {
                PremisMetadata.ParseDuration(test.Key).Should().Be(test.Value);
            }

        }
        
        
    }
}