using FluentAssertions;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.Tests.AssetDomain;

public class PronomTests
{
    [Fact]
    public void Pronom_Dict_Created_On_Demand()
    {
        var map = PronomData.Instance.FormatMap;

        map.Should().NotBeNull();
        map.Count.Should().BePositive();
        map["fmt/42"].Should().Be("image/jpeg");
    }

    [Fact]
    public void Pronom_Dict_Returns_First_MimeType()
    {
        // "fmt/340": "application/lwp, application/vnd.lotus-wordpro",
        
        var map = PronomData.Instance.FormatMap;

        map["fmt/340"].Should().Be("application/lwp");
    }
}