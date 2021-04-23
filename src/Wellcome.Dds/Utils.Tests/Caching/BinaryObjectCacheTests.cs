using System;
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

        private const string ContainerName = "container";

        public BinaryObjectCacheTests()
        {
            storage = A.Fake<IStorage>();
            memoryCache = A.Fake<IMemoryCache>();
        }

        private BinaryObjectCache<FakeStoredFileInfo> GetSut(bool hasMemoryCache = true, int cacheSeconds = 1, bool avoidCaching = false,
            bool avoidSaving = false)
        {
            var fakeStoredFileInfoOptions = new BinaryObjectCacheOptions
            {
                Prefix = "tst-",
                MemoryCacheSeconds = cacheSeconds,
                AvoidCaching = avoidCaching,
                AvoidSaving = avoidSaving,
                Container = ContainerName
            };
            var byType = new BinaryObjectCacheOptionsByType();
            byType["Utils.Tests.Caching.FakeStoredFileInfo"] = fakeStoredFileInfoOptions;

            var options = Options.Create(byType);

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
            A.CallTo(() => storage.GetCachedFileInfo(ContainerName, expected)).MustHaveHappened();
        }

        [Fact]
        public void GetCachedFile_ReturnsFileFromStorage()
        {
            // Arrange
            const string key = nameof(GetCachedFile_ReturnsFileFromStorage);

            var fileInfo = new FakeStoredFileInfo();
            A.CallTo(() => storage.GetCachedFileInfo(ContainerName, A<string>._)).Returns(fileInfo);
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
            A.CallTo(() => storage.DeleteCacheFile(ContainerName, expected)).MustHaveHappened();
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
            A.CallTo(() => storage.DeleteCacheFile(ContainerName, expectedFileName)).MustHaveHappened();
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
        
        [Fact]
        public async Task GetCachedObjectFromLocal_ReturnsObjectFromMemoryCache_IfFound()
        {
            // Arrange
            const string key = nameof(GetCachedObjectFromLocal_ReturnsObjectFromMemoryCache_IfFound);
            var sut = GetSut();
            object expectedMemoryCache = $"tst-{key}";
            var expected = new FakeStoredFileInfo();
            
            object output;
            A.CallTo(() => memoryCache.TryGetValue(expectedMemoryCache, out output))
                .Returns(true)
                .AssignsOutAndRefParameters(expected);
            
            // Act
            var result = await sut.GetCachedObjectFromLocal(key, () => Task.FromResult(expected));

            // Assert
            result.Should().Be(expected);
            A.CallTo(() => memoryCache.TryGetValue(expectedMemoryCache, out output))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task GetCachedObjectFromLocal_ReturnsAndSavesResultOfDelegate()
        {
            // Arrange
            const string key = nameof(GetCachedObjectFromLocal_ReturnsAndSavesResultOfDelegate);
            var sut = GetSut();
            var expected = new FakeStoredFileInfo();
            
            // Act
            var result = await sut.GetCachedObjectFromLocal(key, () => Task.FromResult(expected));

            // Assert
            result.Should().Be(expected);
            A.CallTo(() => memoryCache.CreateEntry(A<object>._))
                .MustHaveHappened();
            A.CallTo(() => storage.Read<FakeStoredFileInfo>(A<ISimpleStoredFileInfo>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.Write(expected, A<ISimpleStoredFileInfo>._, A<bool>._))
                .MustHaveHappened();
        }
    }
    
    public class FakeStoredFileInfo : ISimpleStoredFileInfo
    {
        public string Uri { get; }
        public Task<bool> DoesExist()
        {
            throw new NotImplementedException();
        }

        public Task<DateTime?> GetLastWriteTime()
        {
            throw new NotImplementedException();
        }

        public string Container { get; }
        public string Path { get; }

        public FakeStoredFileInfo(string path = "my_test_path")
        {
            Path = path;
        }
    }
}