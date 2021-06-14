using FluentAssertions;
using Wellcome.Dds.Repositories.Presentation;
using Xunit;

namespace Wellcome.Dds.Tests.IIIFBuilding
{
    public class MetsConversionTests
    {
        [Theory]
        // structDiv  LABEL     TYPE              EXPECTED 
        [InlineData(  "Tape 1", "Side1",          "Tape 1, Side 1")]
        [InlineData(  "Tape 1", "Side 1",         "Tape 1, Side 1")]
        [InlineData(  "Side 1", "Side1",          "Side 1")]
        [InlineData(  "Side 1", "side 1",         "Side 1")]
        [InlineData(  "Reel 3", "",               "Reel 3")]
        [InlineData(  "Reel 3", "Side1",          "Reel 3, Side 1")]
        [InlineData(  "Part of side 1", "Side1",  "Part of side 1")]
        [InlineData(  "Disc 1", "A-side",         "Disc 1")]
        public void TestSyntheticCanvasLabels(string label, string type, string expected)
        {
            var betterLabel = IIIFBuilderParts.GetBetterAVCanvasLabel(label, type);
            betterLabel.Should().Be(expected);
        }
    }
}