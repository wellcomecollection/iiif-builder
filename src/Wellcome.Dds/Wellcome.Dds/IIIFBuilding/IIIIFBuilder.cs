using System.Threading.Tasks;
using IIIF.Presentation;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.WordsAndPictures.SimpleAltoServices;

namespace Wellcome.Dds.IIIFBuilding
{
    public interface IIIIFBuilder
    {
        /// <summary>
        /// Builds the IIIF Manifest or Collection for the given identifier.
        /// This might just be one volume in a multi-volume work.
        /// It will not save it anywhere, that's for you to do.
        /// 
        /// This is suitable for refreshing/previewing a single
        /// manifestation build.
        /// </summary>
        /// <param name="identifier">e.g., b12345678, b87654321_0001</param>
        /// <param name="work">If you already have a work, the class will use it, otherwise it will acquire it from the catalogue</param>
        /// <returns></returns>
        public Task<MultipleBuildResult> Build(string identifier, Work work = null);
        
        /// <summary>
        /// Builds ALL Manifests and Collections for the given bNumber.
        /// For a single volume work this does the same as Build(..),
        /// but for a multi-volume work, it will traverse and build all.
        ///
        /// This will Save them as it goes (e.g., to S3, or whatever its impl provides)
        ///
        /// This is suitable for rebuilding, as it ensures all manifests are updated
        /// for a work, and collection membership is correct.
        /// </summary>
        /// <param name="bNumber"></param>
        /// <param name="work">If you already have a work, the class will use it, otherwise it will acquire it from the catalogue</param>
        /// <returns></returns>
        public Task<MultipleBuildResult> BuildAllManifestations(string bNumber, Work work = null);

 
        string Serialise(ResourceBase iiifResource);
        AltoAnnotationBuildResult BuildW3CAnnotations(IManifestation manifestation, AnnotationPageList annotationPages);
    }
}
