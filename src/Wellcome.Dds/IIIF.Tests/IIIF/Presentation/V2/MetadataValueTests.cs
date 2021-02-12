using System.Collections.Generic;
using FluentAssertions;
using IIIF.Presentation.V2;
using IIIF.Presentation.V3.Strings;
using Xunit;

namespace IIIF.Tests.IIIF.Presentation.V2
{
    public class MetadataValueTests
    {
        [Fact]
        public void CtorFromLanguageMap_ReturnsNull_IfPassedNull() 
            => MetaDataValue.Create((LanguageMap)null).Should().BeNull();

        [Fact]
        public void CtorFromLanguageMap_LanguageValuesNullOrEmpty_IfPassedEmptyMap()
        {
            // Arrange
            var languageMap = new LanguageMap();

            // Act
            var metadataValue = MetaDataValue.Create(languageMap);
            
            // Assert
            metadataValue.LanguageValues.Should().BeNullOrEmpty();
        }
        
        [Fact]
        public void CtorFromLanguageMap_Handles_SingleLanguageSingleValue()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "foo bar");
            
            var expected = new List<LanguageValue>
            {
                new() {Language = "en", Value = "foo bar"},
            };
            
            // Act
            var metadataValue = MetaDataValue.Create(languageMap);
            
            // Assert
            metadataValue.LanguageValues.Should().BeEquivalentTo(expected).And.HaveCount(1);
        }
        
        [Fact]
        public void CtorFromLanguageMap_Handles_SingleLanguageMultiValue()
        {
            // Arrange
            var languageMap = new LanguageMap("en", new[] {"foo", "bar"});

            var expected = new List<LanguageValue>
            {
                new() {Language = "en", Value = "foo"},
                new() {Language = "en", Value = "bar"}
            };
            
            // Act
            var metadataValue = MetaDataValue.Create(languageMap);
            
            // Assert
            metadataValue.LanguageValues.Should().BeEquivalentTo(expected).And.HaveCount(2);
        }
        
        [Fact]
        public void CtorFromLanguageMap_Handles_MultiLanguage()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "foo");
            languageMap["fr"] = new List<string> {"bar"};

            var expected = new List<LanguageValue>
            {
                new() {Language = "en", Value = "foo"},
                new() {Language = "fr", Value = "bar"}
            };
            
            // Act
            var metadataValue = MetaDataValue.Create(languageMap);
            
            // Assert
            metadataValue.LanguageValues.Should().BeEquivalentTo(expected).And.HaveCount(2);
        }
        
        [Fact]
        public void CtorFromLanguageMap_IgnoresLanguageIfNone()
        {
            // Arrange
            var languageMap = new LanguageMap("none", "foo bar");
            
            var expected = new List<LanguageValue>
            {
                new() {Language = null, Value = "foo bar"},
            };
            
            // Act
            var metadataValue = MetaDataValue.Create(languageMap);
            
            // Assert
            metadataValue.LanguageValues.Should().BeEquivalentTo(expected).And.HaveCount(1);
        }
        
        [Fact]
        public void CtorFromLanguageMap_IgnoresLanguageIf_IgnoreLanguageTrue()
        {
            // Arrange
            var languageMap = new LanguageMap("en", "foo bar");
            
            var expected = new List<LanguageValue>
            {
                new() {Value = "foo bar"},
            };
            
            // Act
            var metadataValue = MetaDataValue.Create(languageMap, true);
            
            // Assert
            metadataValue.LanguageValues.Should().BeEquivalentTo(expected).And.HaveCount(1);
        }
    }
}