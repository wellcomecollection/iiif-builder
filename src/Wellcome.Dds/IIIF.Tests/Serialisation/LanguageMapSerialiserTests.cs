using System.Collections.Generic;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Newtonsoft.Json;
using Xunit;

namespace IIIF.Tests.Serialisation
{
    public class LanguageMapSerialiserTests
    {
        private readonly LanguageMapSerialiser sut;
        public LanguageMapSerialiserTests()
        {
            sut = new LanguageMapSerialiser();
        }
        
        [Fact]
        public void WriteJson_WritesSingleLine_IfSingleLanguageWithSingleShortValue()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "single and short");
            var expected = "{\"en\":[\"single and short\"]}";
            
            // Act
            var result = JsonConvert.SerializeObject(languageMap, Formatting.Indented, sut);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void WriteJson_WritesIndented_IfSingleLanguageWithSingleLongValue()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "this string is slightly longer than the threshold");
            var expected = @"{
  ""en"": [
    ""this string is slightly longer than the threshold""
  ]
}";
            
            // Act
            var result = JsonConvert.SerializeObject(languageMap, Formatting.Indented, sut);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void WriteJson_WritesIndented_IfSingleLanguageWithMultipleShortValues()
        {
            // Arrange
            var languageMap = new LanguageMap("en", new[] {"single and short", "also short"});
            var expected = @"{
  ""en"": [
    ""single and short"",
    ""also short""
  ]
}";
            
            // Act
            var result = JsonConvert.SerializeObject(languageMap, Formatting.Indented, sut);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void WriteJson_WritesIndented_IfMultiLanguageWithShortValues()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "single and short");
            languageMap.Add("fr", new List<string> {"also short"});
            var expected = @"{
  ""en"": [
    ""single and short""
  ],
  ""fr"": [
    ""also short""
  ]
}";
            
            // Act
            var result = JsonConvert.SerializeObject(languageMap, Formatting.Indented, sut);
            
            // Assert
            result.Should().Be(expected);
        }
    }
}