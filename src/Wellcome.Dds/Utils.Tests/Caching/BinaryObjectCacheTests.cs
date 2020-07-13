﻿using System;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utils.Caching;
using Utils.Storage;
using Xunit;

namespace Utils.Tests.Caching
{
    public class BinaryObjectCacheTests
    {
        private readonly IStorage storage;
        private readonly IMemoryCache memoryCache;

        public BinaryObjectCacheTests()
        {
            storage = A.Fake<IStorage>();
            memoryCache = A.Fake<IMemoryCache>();
        }

        private BinaryObjectCache<FakeStoredFileInfo> GetSut(bool hasMemoryCache = true, int cacheSeconds = 1, bool avoidCaching = false,
            bool avoidSaving = false)
        {
            var binaryObjectCacheOptions = new BinaryObjectCacheOptions
            {
                Prefix = "tst-",
                MemoryCacheSeconds = cacheSeconds,
                AvoidCaching = avoidCaching,
                AvoidSaving = avoidSaving
            };
            var options = Options.Create(binaryObjectCacheOptions);

            return new BinaryObjectCache<FakeStoredFileInfo>(new NullLogger<BinaryObjectCache<FakeStoredFileInfo>>(),
                options, storage, hasMemoryCache ? memoryCache : null);
        }

        [Fact]
        public void GetCachedFile_GetsExpectedFileFromStorage()
        {
            // Arrange
            const string key = nameof(GetCachedFile_GetsExpectedFileFromStorage);
            var expected = $"{key}.ser";
            var sut = GetSut(); 
            
            // Act
            sut.GetCachedFile(key);
            
            // Assert
            A.CallTo(() => storage.GetCachedFile(expected)).MustHaveHappened();
        }
        
        [Fact]
        public void GetCachedFile_ReturnsFileFromStorage()
        {
            // Arrange
            const string key = nameof(GetCachedFile_ReturnsFileFromStorage);

            var fileInfo = new FakeStoredFileInfo();
            A.CallTo(() => storage.GetCachedFile(A<string>._)).Returns(fileInfo);
            var sut = GetSut();

            // Act
            var actual = sut.GetCachedFile(key);
            
            // Assert
            actual.Should().Be(fileInfo);
        }

        [Fact]
        public async Task DeleteCacheFile_DeletesExpectedCachedFile_MemoryCacheNull()
        {
            // Arrange
            const string key = nameof(DeleteCacheFile_DeletesExpectedCachedFile_MemoryCacheNull);
            var expected = $"{key}.ser";
            var sut = GetSut(hasMemoryCache: false);
            
            // Act
            await sut.DeleteCacheFile(key);
            
            // Assert
            A.CallTo(() => storage.DeleteCacheFile(expected)).MustHaveHappened();
        }
        
        [Fact]
        public async Task DeleteCacheFile_DeletesExpected_FromStorageAndMemory()
        {
            // Arrange
            const string key = nameof(DeleteCacheFile_DeletesExpectedCachedFile_MemoryCacheNull);
            var expectedMemoryCache = $"tst-{key}";
            var expectedFileName = $"{key}.ser";
            var sut = GetSut();
            
            // Act
            await sut.DeleteCacheFile(key);
            
            // Assert
            A.CallTo(() => memoryCache.Remove(expectedMemoryCache)).MustHaveHappened();
            A.CallTo(() => storage.DeleteCacheFile(expectedFileName)).MustHaveHappened();
        }

        [Fact]
        public async Task GetCachedObject_AvoidCaching_ReturnsDefaultForType_IfDelegateIsNull()
        {
            // Arrange
            const string key = nameof(GetCachedObject_AvoidCaching_ReturnsDefaultForType_IfDelegateIsNull);
            var sut = GetSut(avoidCaching: true);
            
            // Act
            var result = await sut.GetCachedObject(key, null);

            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public async Task GetCachedObject_AvoidCaching_ReturnsDefaultForType_IfDelegateReturnsNull()
        {
            // Arrange
            const string key = nameof(GetCachedObject_AvoidCaching_ReturnsDefaultForType_IfDelegateReturnsNull);
            var sut = GetSut(avoidCaching: true);
            
            // Act
            var result = await sut.GetCachedObject(key, () => Task.FromResult((FakeStoredFileInfo)null));

            // Assert
            result.Should().BeNull();
            A.CallTo(() => storage.Write(A<FakeStoredFileInfo>._, A<ISimpleStoredFileInfo>._, A<bool>._))
                .MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetCachedObject_AvoidCaching_ReturnsResultOfDelegateWithoutSaving_IfAvoidSaving()
        {
            // Arrange
            const string key = nameof(GetCachedObject_AvoidCaching_ReturnsResultOfDelegateWithoutSaving_IfAvoidSaving);
            var sut = GetSut(avoidCaching: true, avoidSaving: true);
            var expected = new FakeStoredFileInfo();
            
            // Act
            var result = await sut.GetCachedObject(key, () => Task.FromResult(expected));

            // Assert
            result.Should().Be(expected);
            A.CallTo(() => storage.Write(A<FakeStoredFileInfo>._, A<ISimpleStoredFileInfo>._, A<bool>._))
                .MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetCachedObject_AvoidCaching_ReturnsAndSavesResultOfDelegate()
        {
            // Arrange
            const string key = nameof(GetCachedObject_AvoidCaching_ReturnsResultOfDelegateWithoutSaving_IfAvoidSaving);
            var sut = GetSut(avoidCaching: true);
            var expected = new FakeStoredFileInfo();
            
            // Act
            var result = await sut.GetCachedObject(key, () => Task.FromResult(expected));

            // Assert
            result.Should().Be(expected);
            A.CallTo(() => storage.Write(expected, A<ISimpleStoredFileInfo>._, A<bool>._))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task GetCachedObject_ReturnsObjectFromMemoryCache_IfFound()
        {
            // Arrange
            const string key = nameof(GetCachedObject_ReturnsObjectFromMemoryCache_IfFound);
            var sut = GetSut();
            object expectedMemoryCache = $"tst-{key}";
            var expected = new FakeStoredFileInfo();
            
            object output;
            A.CallTo(() => memoryCache.TryGetValue(expectedMemoryCache, out output))
                .Returns(true)
                .AssignsOutAndRefParameters(expected);
            
            // Act
            var result = await sut.GetCachedObject(key, () => Task.FromResult(expected));

            // Assert
            result.Should().Be(expected);
            A.CallTo(() => memoryCache.TryGetValue(expectedMemoryCache, out output))
                .MustHaveHappened();
        }
    }
    
    public class FakeStoredFileInfo : ISimpleStoredFileInfo
    {
        public DateTime LastWriteTime { get; }
        public string Uri { get; }
        public bool Exists { get; }
        public string Container { get; }
        public string Path { get; }

        public FakeStoredFileInfo(string path = "my_test_path")
        {
            Path = path;
        }
    }
}