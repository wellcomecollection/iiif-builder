using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IIIF;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V3;
using IIIF.Search.V1;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.Search;
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
        public Task<MultipleBuildResult> Build(string identifier, Work? work = null);

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
        public Task<MultipleBuildResult> BuildAllManifestations(string bNumber, Work? work = null);

        /// <summary>
        /// Build IIIF Presentation 2 representation from provided IIIF Presentation 3 BuildResults.
        /// Only V3 results will be processed, any V2 will be ignored.
        /// </summary>
        /// <param name="identifier">e.g., b12345678, b87654321_0001</param>
        /// <param name="buildResults">IIIF 3 build results</param>
        public MultipleBuildResult BuildLegacyManifestations(string identifier, IEnumerable<BuildResult> buildResults);

        AltoAnnotationBuildResult BuildW3CAndOaAnnotations(IManifestation manifestation, AnnotationPageList annotationPages);
        
        TermList BuildTermListV1(string manifestationIdentifier, string q, string[] suggestions);
        
        SearchResultAnnotationList BuildSearchResultsV0(Text text, IEnumerable<SearchResult> results,
            string manifestationIdentifier, string s);

        SearchResultAnnotationList BuildSearchResultsV1(Text text, string manifestationIdentifier, string s);
        
        /// <summary>
        /// A dynamically parsed OA anno list.
        /// TODO: We need to consider this approach (might not be a good idea).
        /// </summary>
        /// <param name="v3">A dynamically parsed JSON object</param>
        /// <returns>A V2 (Open Annotation) AnnotatioList</returns>
        AnnotationList ConvertW3CAnnoPageJsonToOAAnnoList(JObject v3, string manifestationIdentifier, string assetIdentifier);
    }
}
