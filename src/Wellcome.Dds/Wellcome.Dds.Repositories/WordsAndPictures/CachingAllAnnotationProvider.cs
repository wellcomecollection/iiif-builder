using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.WordsAndPictures.SimpleAltoServices;

namespace Wellcome.Dds.Repositories.WordsAndPictures
{
    /// <summary>
    /// Keeps cached lists of annotation per manifest, to avoid having to rebuild them too many times.
    /// </summary>
    public class CachingAllAnnotationProvider
    {
        private readonly IBinaryObjectCache<AnnotationPageList> cache; // needs options allAnnotationCache, "annopages_", 0
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly ILogger<CachingAllAnnotationProvider> logger;

        public CachingAllAnnotationProvider(
            
            IBinaryObjectCache<AnnotationPageList> cache,
            IWorkStorageFactory workStorageFactory,
            ILogger<CachingAllAnnotationProvider> logger)
        {
            this.workStorageFactory = workStorageFactory;
            this.cache = cache;
            this.logger = logger;
        }

        public Task<AnnotationPageList?> GetPages(
            string identifier,
            IEnumerable<IPhysicalFile> physicalFiles)
        {
            return cache.GetCachedObject(identifier, () => GetPagesInternal(identifier, physicalFiles));
        }

        public Task<AnnotationPageList?> ForcePagesRebuild(
            string identifier,
            IEnumerable<IPhysicalFile> physicalFiles)
        {
            return cache.GetCachedObjectFromLocal(identifier, () => GetPagesInternal(identifier, physicalFiles));
        }

        private async Task<AnnotationPageList?> GetPagesInternal(
            string identifier, IEnumerable<IPhysicalFile> physicalFiles)
        {
            logger.LogInformation($"Building Annotation Pages for {identifier}");
            var altoProvider = new SimpleAltoProvider(logger);
            var pages = new AnnotationPageList();
            var workStore = await workStorageFactory.GetWorkStore(identifier);
            foreach (var physicalFile in physicalFiles)
            {
                if (physicalFile.RelativeAltoPath.HasText())
                {
                    XElement? altoRoot = null;
                    try
                    {
                        var altoXml = await workStore.LoadXmlForPath(physicalFile.RelativeAltoPath, false);
                        altoRoot = altoXml.XElement;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Cannot read or parse ALTO in {relativeAltoPath}",
                            physicalFile.RelativeAltoPath);
                    }

                    var page = altoProvider.GetAnnotationPage(altoRoot,
                            physicalFile.AssetMetadata.GetImageWidth(),
                            physicalFile.AssetMetadata.GetImageHeight(),
                            identifier,
                            physicalFile.StorageIdentifier,
                            (physicalFile.Order ?? 1) - 1);
                    logger.LogInformation($"Adding page - {page}");
                    pages.Add(page);
                }
            }
            return pages;
        }
        public ISimpleStoredFileInfo GetFileInfo(string identifier)
        {
            return cache.GetCachedFile(identifier);
        }

        public void DeleteAnnotationCache(string identifier)
        {
            cache.DeleteCacheFile(identifier);
        }
        
        
        public void DisableMemoryCache()
        {
            cache.DisableMemoryCache();
        }

        public void EnableMemoryCache()
        {
            cache.EnableMemoryCache();
        }
    }
}
