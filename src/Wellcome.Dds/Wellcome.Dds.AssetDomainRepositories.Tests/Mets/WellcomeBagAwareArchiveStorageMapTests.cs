﻿using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Tests.Samples;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class WellcomeBagAwareArchiveStorageMapTests
    {
        [Fact]
        public void FromJObject_ReturnsExpected_SingleVersion()
        {
            // Arrange
            var json = SampleHelpers.GetJson("single_version.json");
            const string identifier = "b12345678";

            var expected = new WellcomeBagAwareArchiveStorageMap
            {
                Identifier = identifier,
                BucketName = "main-storage",
                StorageManifestCreated = new DateTime(2019, 9, 13, 10, 10, 0),
                VersionSets = new List<KeyValuePair<string, HashSet<string>>>
                {
                    new KeyValuePair<string, HashSet<string>>("v1", new HashSet<string>
                    {
                        "#.xml", "objects/#_0001.jp2", "objects/0002.jp2"
                    })
                }
            };
            
            // Act
            var actual = WellcomeBagAwareArchiveStorageMap.FromJObject(json, identifier);
            
            // Assert
            actual.Should().BeEquivalentTo(expected, options => options.Excluding(b => b.Built));
            actual.Built.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
        
        [Fact]
        public void FromJObject_ReturnsExpected_MultipleVersion()
        {
            // Arrange
            var json = SampleHelpers.GetJson("multi_version.json");
            const string identifier = "b12345678";

            var expected = new WellcomeBagAwareArchiveStorageMap
            {
                Identifier = identifier,
                BucketName = "main-storage",
                StorageManifestCreated = new DateTime(2019, 9, 13, 10, 10, 0),
                VersionSets = new List<KeyValuePair<string, HashSet<string>>>
                {
                    new KeyValuePair<string, HashSet<string>>(
                        "v1", new HashSet<string>
                        {
                            "#_0001.xml", "alto/#_0001.xml", "objects/#_0001_0001.jp2", "objects/#_0001_0002.jp2",
                            "objects/#_0001_0003.jp2"
                        }),
                    new KeyValuePair<string, HashSet<string>>(
                        "v2", new HashSet<string>
                        {
                            "#.xml", "#_0002.xml", "objects/#_0002_0001.jp2", "objects/#_0002_0002.jp2",
                            "objects/#_0002_0003.jp2", "objects/#_0002_0004.jp2"
                        })
                }
            };
            
            // Act
            var actual = WellcomeBagAwareArchiveStorageMap.FromJObject(json, identifier);
            
            // Assert
            actual.Should().BeEquivalentTo(expected, options => options.Excluding(b => b.Built));
            actual.Built.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
        
        [Fact]
        public void FromJObject_ReturnsVersionSets_InIncreasingOrderOfSize()
        {
            // Arrange
            var json = SampleHelpers.GetJson("ordering_test.json");
            const string identifier = "b12345678";

            // Act
            var actual = WellcomeBagAwareArchiveStorageMap.FromJObject(json, identifier);
            
            // Assert
            actual.VersionSets[0].Key.Should().Be("v1");
            actual.VersionSets[1].Key.Should().Be("v2");
        }
    }
}