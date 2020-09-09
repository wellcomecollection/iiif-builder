using System;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.IIIFBuilding
{
    /// <summary>
    /// Here live all the string templates for URLs used in IIIF resources.
    /// This class favours simplicity over absolute perf
    /// https://blog.codinghorror.com/the-sad-tragedy-of-micro-optimization-theater/
    /// ...but, it does a LOT of string replacement.
    /// So we could come back to it and do it differently, later.
    /// </summary>
    public class UriPatterns
    {
        private readonly ICatalogue catalogue;
        private readonly string schemeAndHostValue;
        private const string SchemeAndHostToken = "{schemeAndHost}";
        private const string IdentifierToken = "{identifier}";
        private const string AssetIdentifierToken = "{assetIdentifier}";
        
        // TODO - these constants should be in the IIIF model
        private const string IIIF2PreziContext = "http://iiif.io/api/presentation/2/context.json";
        private const string IIIF2ImageContext = "http://iiif.io/api/image/2/context.json";
        private const string IIIFAuthContext = "http://iiif.io/api/auth/0/context.json";
        private const string IIIFSearchContext = "http://iiif.io/api/search/0/context.json";

        private const string ImageServiceProfile = "http://iiif.io/api/image/2/level1.json";
        private const string ImageServiceLevel0Profile = "http://iiif.io/api/image/2/level0.json";
        private const string LoginServiceProfile = "http://iiif.io/api/auth/0/login";
        private const string LoginClickthroughServiceProfile090 = "http://iiif.io/api/auth/0/login/clickthrough";
        private const string LoginClickthroughServiceProfile093 = "http://iiif.io/api/auth/0/clickthrough";
        private const string LoginExternalServiceProfile090 = "http://iiif.io/api/auth/0/login/restricted";
        private const string LoginExternalServiceProfile093 = "http://iiif.io/api/auth/0/external";
        private const string LogoutServiceProfile = "http://iiif.io/api/auth/0/logout";
        private const string AuthTokenServiceProfile = "http://iiif.io/api/auth/0/token";

        // These patterns belong with the DDS, here
        // but they need to change to the suggested forms in
        // https://github.com/wellcomecollection/platform/issues/4659#issuecomment-686448554
        
        private const string ManifestFormat = "{schemeAndHost}/presentation/{identifier}";
        private const string CanvasFormat = "{schemeAndHost}/presentation/{identifier}/canvases/{assetIdentifier}";
        private const string AggregationFormat = "{schemeAndHost}/presentation/collections";
        
        // not done
        private const string AnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annotation/{name}";
        private const string AnnotationListFormat = "{schemeAndHost}/{prefix}/{identifier}/list/{name}";
        private const string RangeFormat = "{schemeAndHost}/{prefix}/{identifier}/range/{name}";
        private const string LayerFormat = "{schemeAndHost}/{prefix}/{identifier}/layer/{name}";
        private const string ContentFormat = "{schemeAndHost}/{prefix}/{identifier}/res/{name}.{format}";

        // Image service URI
        private const string ImageResourceFormat = "{schemeAndHost}/{prefix}/{identifier}-{seqIndex}/res/{name}";

        private const string ImageServiceFormat = "{schemeAndHost}/{prefix}-img/{identifier}-{seqIndex}/{name}";
        private const string ImageAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/imageanno/{name}";
        private const string OcrAltoAllAnnosFormat = "{schemeAndHost}/{prefix}/{identifier}/{name}";
        private const string OcrAltoContentFormat = "{schemeAndHost}/{prefix}/{identifier}/contentAsText/{name}";
        private const string TextLineAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annos/contentAsText/{name}";
        private const string SearchResultAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annos/searchResults/{name}";
        private const string ManifestLevelServiceFormat = "{schemeAndHost}/{prefix}/{identifier}-{seqIndex}/{name}-service";
        
        // TODO - rename to WorkPageFormat, once fully ported.
        private const string PersistentPlayerUriFormat = "https://wellcomecollection.org/works/{identifier}";
        private const string PersistentCatalogueRecordFormat = "https://search.wellcomelibrary.org/iii/encore/record/C__R{identifier}";
        private const string EncoreBibliographicDataFormat = "https://search.wellcomelibrary.org/iii/queryapi/collection/bib/{identifier}?profiles=b(full)i(brief)&amp;format=xml";
        
        public UriPatterns(
            IOptions<DdsOptions> ddsOptions,
            ICatalogue catalogue)
        {
            schemeAndHostValue = ddsOptions.Value.LinkedDataDomain;
            this.catalogue = catalogue;
        }

        public string Manifest(string identifier)
        {
            return ManifestFormat
                .Replace(SchemeAndHostToken, schemeAndHostValue)
                .Replace(IdentifierToken, identifier);
        }

        public string CollectionForWork(string identifier)
        {
            // yes, these two are the same. Do we want to reserve separate usage?
            return Manifest(identifier);
        }
        
        
        public string Canvas(string manifestIdentifier, string assetIdentifier)
        {
            return CanvasFormat
                .Replace(SchemeAndHostToken, schemeAndHostValue)
                .Replace(IdentifierToken, manifestIdentifier)
                .Replace(AssetIdentifierToken, assetIdentifier);
        }

        public string CollectionForAggregation()
        {
            return AggregationFormat.Replace(SchemeAndHostToken, schemeAndHostValue);
        }

        public string CollectionForAggregation(string aggregator)
        {
            return $"{CollectionForAggregation()}/{aggregator}";
        }
        
        public string CollectionForAggregation(string aggregator, string value)
        {
            return $"{CollectionForAggregation()}/{aggregator}/{value}";
        }
        
        // TODO - rename to Work page
        /// <summary>
        /// Must be a works identifier, not a b number
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public string PersistentPlayerUri(string identifier)
        {
            return PersistentPlayerUriFormat.Replace(IdentifierToken, identifier);
        }

        public string PersistentCatalogueRecord(string identifier)
        {
            return PersistentCatalogueRecordFormat.Replace(IdentifierToken, identifier.Remove(8));
        }

        public string EncoreBibliographicData(string identifier)
        {
            return EncoreBibliographicDataFormat.Replace(IdentifierToken, identifier.Remove(8));
        }

        public string CatalogueApi(string workIdentifier, string[] includes)
        {
            return catalogue.GetCatalogueApiUrl(workIdentifier, includes);
        }
        
        
        
    }
}