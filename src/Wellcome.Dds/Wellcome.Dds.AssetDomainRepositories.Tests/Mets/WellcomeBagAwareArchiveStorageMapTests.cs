using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomain;
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
        public void GetDeletion_RecognisesGoobiDeletionBag()
        {
            // A Goobi deletion: version v2 has no files and a payload of 0 bytes in 0 files
            var json = SampleHelpers.GetJson("deletion_bag.json");

            var deletion = WellcomeBagAwareArchiveStorageMap.GetDeletion(json, "b10985402");

            deletion.Should().NotBeNull();
            deletion.Identifier.Should().Be("b10985402");
            deletion.Version.Should().Be("v2");
            deletion.BaggingDate.Should().Be("2026-10-26");
            deletion.SenderDescription.Should().Be("2026-10-26_b10985402_deletion");
            deletion.Message.Should().Be("b10985402 was deleted in the storage service: its latest version " +
                                         "(v2, bagged 2026-10-26, \"2026-10-26_b10985402_deletion\") has no files, " +
                                         "so there is nothing to build.");
            WellcomeBagAwareArchiveStorageMap.FromJObject(json, "b10985402").VersionSets.Should().BeEmpty();
        }

        [Theory]
        [InlineData("single_version.json")]
        [InlineData("multi_version.json")]
        public void GetDeletion_IsNull_ForBagsWithFiles(string sample)
        {
            WellcomeBagAwareArchiveStorageMap.GetDeletion(SampleHelpers.GetJson(sample), "b12345678")
                .Should().BeNull();
        }

        [Fact]
        public void GetDeletion_IsNull_WhenNoFilesButPayloadIsNotEmpty()
        {
            // Not a deletion we recognise; the DDS reports it as before
            var json = SampleHelpers.GetJson("deletion_bag.json");
            json["info"]!["payloadOxum"] = "1234.5";

            WellcomeBagAwareArchiveStorageMap.GetDeletion(json, "b10985402").Should().BeNull();
        }

        [Fact]
        public void DeletedInStorageException_Find_LooksThroughInnerExceptions()
        {
            var deletion = new DeletedInStorageException("b10985402", "v2", "2026-10-26", "x_deletion");
            var wrapped = new AggregateException(new InvalidOperationException("outer", deletion));

            DeletedInStorageException.Find(wrapped).Should().BeSameAs(deletion);
            DeletedInStorageException.Find(new InvalidOperationException("other")).Should().BeNull();
            DeletedInStorageException.Find(null).Should().BeNull();
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