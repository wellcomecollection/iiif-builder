﻿using System;
using System.Collections.Generic;
using FluentAssertions;
using IIIF;
using IIIF.Presentation;
using IIIF.Search.V2;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.V2;
using Xunit;
using Presi3 = IIIF.Presentation.V3;
using Presi2 = IIIF.Presentation.V2;

namespace Wellcome.Dds.Repositories.Tests.Presentation.V2
{
    public class PresentationConverterTests
    {
        private readonly PresentationConverter sut;
        private readonly IIdentityService identityService;

        public PresentationConverterTests()
        {
            var options = Options.Create(new DdsOptions
            {
                LinkedDataDomain = "http://test.example/",
                WellcomeCollectionApi = "(unused in this test)",
                ApiWorkTemplate = "(unused in this test)"
            });
            identityService = new ParsingIdentityService(new NullLogger<ParsingIdentityService>(), new MemoryCache(new MemoryCacheOptions()));
            sut = new PresentationConverter(new UriPatterns(options), NullLogger.Instance);
        }
        
        [Fact]
        public void Convert_Throws_IfPassedNull()
        {
            // Arrange
            Action action = () => sut.Convert(null!,  identityService.GetIdentity("b10727000"));

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }
        
        [Fact]
        public void Convert_Throws_IfNonManifestOrCollection()
        {
            // Arrange
            Action action = () => sut.Convert(new Presi3.Canvas(), identityService.GetIdentity("b10727000"));

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Convert_FromManifest_Minimum()
        {
            // Arrange
            var manifest = new Presi3.Manifest
            {
                Id = "/presentation/b12312312",
                Items = new List<Presi3.Canvas>{new ()}
            };
            manifest.EnsureContext(Context.Presentation3Context);

            // Act
            var result = sut.Convert(manifest, identityService.GetIdentity("b10727000"));
            
            // Assert
            result.Context.Should().BeOfType<List<string>>().Which.Should().Contain(Context.Presentation2Context);
        }
        
        [Fact]
        public void Convert_FromCollection_Minimum()
        {
            // Arrange
            var collection = new Presi3.Collection
            {
                Id = "/presentation/b12312312",
                Items = new List<Presi3.ICollectionItem>
                {
                    new Presi3.Manifest
                    {
                        Id = "/presentation/b12312312",
                        Items = new List<Presi3.Canvas>{new ()}
                    }
                }
            };
            collection.EnsureContext(Context.Presentation3Context);

            // Act
            var result = sut.Convert(collection, identityService.GetIdentity("b10727000"));
            
            // Assert
            result.Context.Should().BeOfType<string>().Which.Should().Be(Context.Presentation2Context);
        }
        
        [Fact]
        public void Convert_DoesNotCopyServices_ByReference()
        {
            // Arrange
            var searchService = new SearchService {Id = "test_searchservice"};
            var manifest = new Presi3.Manifest
            {
                Id = "/presentation/b12312312",
                Service = new List<IService> {searchService},
                Items = new List<Presi3.Canvas>{new ()}
            };

            // Act
            var result = sut.Convert(manifest, identityService.GetIdentity("b10727000"));
            var p2Manifest = result as Presi2.Manifest;
            
            // Assert
            var service = p2Manifest.Service[0];
            service.Id.Should().Be(searchService.Id);
            service.Should().NotBe(searchService);
        }

        [Fact]
        public void Convert_DoesNotCopyService_ByReference()
        {
            // Arrange
            var searchService = new SearchService {Id = "test_searchservice"};
            var manifest = new Presi3.Manifest
            {
                Id = "/presentation/b12312312",
                Service = new List<IService> {searchService},
                Items = new List<Presi3.Canvas>{new ()}
            };

            // Act
            var result = sut.Convert(manifest, identityService.GetIdentity("b10727000"));
            var p2Manifest = result as Presi2.Manifest;
            
            // Assert
            var service = p2Manifest.Service[0];
            service.Id.Should().Be(searchService.Id);
            service.Should().NotBe(searchService);
        }
    }
}