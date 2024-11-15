﻿using FluentAssertions;
using IIIF.Presentation.V3.Content;
using Wellcome.Dds.Repositories.Presentation;
using Wellcome.Dds.Repositories.Presentation.V2;
using Wellcome.Dds.Repositories.Presentation.V2.IXIF;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Presentation.V2
{
    public class LegacyServiceFactoryTests
    {
        [Fact]
        public void GetLegacyService_Null_IfProfileUnknown()
        {
            // Arrange
            var resource = new ExternalResource("Test") {Profile = "unknown"};
            
            // Act
            var service = LegacyServiceFactory.GetLegacyService("b19818786", resource);
            
            // Assert
            service.Should().BeNull();
        } 
        
        [Fact]
        public void GetLegacyService_TrackingExtension()
        {
            // Arrange
            var resource = new ExternalResource("Test")
            {
                Profile = Constants.Profiles.TrackingExtension,
                Label = Lang.Map("Test Label")
            };
            
            // Act
            var service = LegacyServiceFactory.GetLegacyService("b19818786", resource);
            
            // Assert
            service.Should()
                .BeOfType<TrackingExtensionsService>()
                .Which.TrackingLabel.Should().Be("Test Label");
        }
        
        [Fact]
        public void GetLegacyService_AccessControlHints()
        {
            // Arrange
            var resource = new ExternalResource("Test")
            {
                Profile = Constants.Profiles.AccessControlHints,
                Label = Lang.Map("Test Label")
            };
            
            // Act
            var service = LegacyServiceFactory.GetLegacyService("b19818786", resource);
            
            // Assert
            service.Should()
                .BeOfType<AccessControlHints>()
                .Which.AccessHint.Should().Be("Test Label");
        }  
    }
}