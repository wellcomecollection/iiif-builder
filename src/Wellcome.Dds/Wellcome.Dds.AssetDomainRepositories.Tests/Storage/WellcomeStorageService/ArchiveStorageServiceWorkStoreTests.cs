﻿using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using FakeItEasy;
using FluentAssertions;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.AssetDomainRepositories.Tests.Samples;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Storage.WellcomeStorageService
{
    public class ArchiveStorageServiceWorkStoreTests
    {
        private readonly ArchiveStorageServiceWorkStore sut;
        private readonly IAmazonS3 s3Client;
        private readonly Dictionary<string, XElement> xmlElementCache = new(); 
        
        public ArchiveStorageServiceWorkStoreTests()
        {
            const string identifier = "b12345678";
            var storageMap =
                WellcomeBagAwareArchiveStorageMap.FromJObject(SampleHelpers.GetJson("multi_version.json"), identifier);
            s3Client = A.Fake<IAmazonS3>();

            sut = new ArchiveStorageServiceWorkStore(identifier, storageMap, null, xmlElementCache, s3Client);
        }

        [Fact]
        public async Task LoadXmlForPath_NoUseCache_AlwaysGetsFromS3()
        {
            // Arrange
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .ReturnsLazily(
                    () => new GetObjectResponse {ResponseStream = GenerateStreamFromString("<foo>bar</foo>")});

            // Act
            var result1 = await sut.LoadXmlForPath("b12345678.xml", false);
            var result2 = await sut.LoadXmlForPath("b12345678.xml", false);
            
            // Assert
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
            result1.XElement.Should().BeEquivalentTo(result2.XElement);
            xmlElementCache.Should().HaveCount(1);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadXmlForPath_UseCache_AlwaysGetsFromS3(bool useCacheOnFirst)
        {
            // Arrange
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .ReturnsLazily(
                    () => new GetObjectResponse {ResponseStream = GenerateStreamFromString("<foo>bar</foo>")});

            // Act
            var result1 = await sut.LoadXmlForPath("b12345678.xml", useCacheOnFirst);
            var result2 = await sut.LoadXmlForPath("b12345678.xml", true);
            
            // Assert
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            result1.XElement.Should().BeEquivalentTo(result2.XElement);
            xmlElementCache.Should().HaveCount(1);
        }
        
        [Fact]
        public async Task LoadXmlForPath_UseCache_DifferentPaths_ReturnsDifferentObjects()
        {
            // Arrange
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .ReturnsLazily(
                    () => new GetObjectResponse {ResponseStream = GenerateStreamFromString("<foo>bar</foo>")});

            // Act
            await sut.LoadXmlForPath("b12345678.xml", true);
            await sut.LoadXmlForPath("b12345678_0001.xml", true);
            
            // Assert
            A.CallTo(() => s3Client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
            xmlElementCache.Should().HaveCount(2);
        }
        
        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}