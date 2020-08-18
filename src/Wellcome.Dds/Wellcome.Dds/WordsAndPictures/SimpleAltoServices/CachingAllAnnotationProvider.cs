using System.Collections.Generic;
using System.Threading.Tasks;
using Utils;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    /// <summary>
    /// Keeps cached lists of annotation per manifest, to avoid having to rebuild them too many times.
    /// </summary>
    public class CachingAllAnnotationProvider
    {
        private readonly IBinaryObjectCache<List<AnnotationPage>> cache; // needs options allAnnotationCache, "annopages_", 0
        private readonly IWorkStorageFactory workStorageFactory;

        public CachingAllAnnotationProvider(
            IBinaryObjectCache<List<AnnotationPage>> cache,
            IWorkStorageFactory workStorageFactory)
        {
            this.workStorageFactory = workStorageFactory;
            this.cache = cache;
        }

        public Task<List<AnnotationPage>> GetPages(
            string identifier,
            IEnumerable<IPhysicalFile> physicalFiles)
        {
            return cache.GetCachedObject(identifier, () => GetPagesInternal(identifier, physicalFiles));
        }

        public Task<List<AnnotationPage>> ForcePagesRebuild(
            string identifier,
            IEnumerable<IPhysicalFile> physicalFiles)
        {
            return cache.GetCachedObject(identifier, () => GetPagesInternal(identifier, physicalFiles), x => true);
        }

        private async Task<List<AnnotationPage>> GetPagesInternal(
            string identifier, IEnumerable<IPhysicalFile> physicalFiles)
        {
            var altoProvider = new SimpleAltoProvider();
            var pages = new List<AnnotationPage>();
            var workStore = await workStorageFactory.GetWorkStore(identifier);
            foreach (var physicalFile in physicalFiles)
            {
                if (physicalFile.RelativeAltoPath.HasText())
                {
                    var altoXml = await workStore.LoadXmlForPath(physicalFile.RelativeAltoPath);
                    var altoRoot = altoXml.XElement;
                    pages.Add(altoProvider.GetAnnotationPage(altoRoot, 
                        physicalFile.AssetMetadata.GetImageWidth(), 
                        physicalFile.AssetMetadata.GetImageHeight(), 
                        (physicalFile.Order ?? 1) - 1));
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
    }
}
