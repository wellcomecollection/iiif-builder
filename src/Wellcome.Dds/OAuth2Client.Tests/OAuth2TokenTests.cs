using System;
using FluentAssertions;
using OAuth2;
using Xunit;

namespace OAuth2Client.Tests
{
    public class OAuth2TokenTests
    {
        [Fact]
        public void GetTimeToLive_Correct()
        {
            // Arrange
            var token = new OAuth2Token {ExpiresIn = 120};
            
            // Act
            var ttl = token.GetTimeToLive();
            
            // Assert
            ttl.Should().BeCloseTo(TimeSpan.FromSeconds(120));
        }
    }
}