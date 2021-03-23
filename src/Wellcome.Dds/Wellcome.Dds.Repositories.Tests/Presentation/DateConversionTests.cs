using FluentAssertions;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using Xunit;
using Xunit.Abstractions;

namespace Wellcome.Dds.Repositories.Tests.Presentation
{
    public class DateConversionTests
    {
        private readonly ITestOutputHelper output;

        public DateConversionTests(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        [Theory]
        [InlineData("1973", "1973-01-01T00:00:00.0000000Z")]
        [InlineData("8. December 1973", "1973-12-08T12:00:00.0000000Z")]
        [InlineData("22/29. December 1973", "1973-12-22T12:00:00.0000000Z")]
        public void Chemist_And_Druggist_Parses_To_Utc(string metsDate, string expectedNavDate)
        {
            var cdState = new ChemistAndDruggistState(null);
            var navDateAsDateTime = cdState.GetNavDate(metsDate);
            var navDateAsString = navDateAsDateTime.ToString("O");
            
            output.WriteLine(navDateAsString);
            navDateAsString.Should().Be(expectedNavDate);
        }
    }
}