using System;
using Microsoft.Extensions.Options;
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
        private readonly string schemeAndHostValue;
        private readonly string apiWorkTemplate;
        private const string SchemeAndHostToken = "{schemeAndHost}";
        private const string IdentifierToken = "{identifier}";
        
        // TODO - these constants should be in the IIIF model
        public const string IIIF2PreziContext = "http://iiif.io/api/presentation/2/context.json";
        public const string IIIF2ImageContext = "http://iiif.io/api/image/2/context.json";
        public const string IIIFAuthContext = "http://iiif.io/api/auth/0/context.json";
        public const string IIIFSearchContext = "http://iiif.io/api/search/0/context.json";

        public const string ImageServiceProfile = "http://iiif.io/api/image/2/level1.json";
        public const string ImageServiceLevel0Profile = "http://iiif.io/api/image/2/level0.json";
        public const string LoginServiceProfile = "http://iiif.io/api/auth/0/login";
        public const string LoginClickthroughServiceProfile090 = "http://iiif.io/api/auth/0/login/clickthrough";
        public const string LoginClickthroughServiceProfile093 = "http://iiif.io/api/auth/0/clickthrough";
        public const string LoginExternalServiceProfile090 = "http://iiif.io/api/auth/0/login/restricted";
        public const string LoginExternalServiceProfile093 = "http://iiif.io/api/auth/0/external";
        public const string LogoutServiceProfile = "http://iiif.io/api/auth/0/logout";
        public const string AuthTokenServiceProfile = "http://iiif.io/api/auth/0/token";

        // These patterns belong with the DDS, here
        // but they need to change to the suggested forms in
        // https://github.com/wellcomecollection/platform/issues/4659#issuecomment-686448554
        
        // yes, these two are the same. Do we want to reserve separate usage?
        public const string CollectionFormat = "{schemeAndHost}/presentation/{identifier}";
        public const string ManifestFormat = "{schemeAndHost}/presentation/{identifier}";
        
        public const string SequenceFormat = "{schemeAndHost}/{prefix}/{identifier}/sequence/{name}";
        public const string CanvasFormat = "{schemeAndHost}/{prefix}/{identifier}/canvas/{name}";
        public const string AnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annotation/{name}";
        public const string AnnotationListFormat = "{schemeAndHost}/{prefix}/{identifier}/list/{name}";
        public const string RangeFormat = "{schemeAndHost}/{prefix}/{identifier}/range/{name}";
        public const string LayerFormat = "{schemeAndHost}/{prefix}/{identifier}/layer/{name}";
        public const string ContentFormat = "{schemeAndHost}/{prefix}/{identifier}/res/{name}.{format}";

        // Image service URI
        public const string ImageResourceFormat = "{schemeAndHost}/{prefix}/{identifier}-{seqIndex}/res/{name}";

        public const string ImageServiceFormat = "{schemeAndHost}/{prefix}-img/{identifier}-{seqIndex}/{name}";
        public const string ImageAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/imageanno/{name}";
        public const string OcrAltoAllAnnosFormat = "{schemeAndHost}/{prefix}/{identifier}/{name}";
        public const string OcrAltoContentFormat = "{schemeAndHost}/{prefix}/{identifier}/contentAsText/{name}";
        public const string TextLineAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annos/contentAsText/{name}";
        public const string SearchResultAnnotationFormat = "{schemeAndHost}/{prefix}/{identifier}/annos/searchResults/{name}";
        public const string ManifestLevelServiceFormat = "{schemeAndHost}/{prefix}/{identifier}-{seqIndex}/{name}-service";
        
        // TODO - rename to work page
        public const string PersistentPlayerUriFormat = "https://wellcomecollection.org/works/{identifier}";
        public const string PersistentCatalogueRecordFormat = "https://search.wellcomelibrary.org/iii/encore/record/C__R{identifier}";
        public const string EncoreBibliographicDataFormat = "https://search.wellcomelibrary.org/iii/queryapi/collection/bib/{identifier}?profiles=b(full)i(brief)&amp;format=xml";
        
        public UriPatterns(IOptions<DdsOptions> ddsOptions)
        {
            schemeAndHostValue = ddsOptions.Value.LinkedDataDomain;
            apiWorkTemplate = ddsOptions.Value.ApiWorkTemplate;
        }

        public string Manifest(string identifier)
        {
            return ManifestFormat
                .Replace(SchemeAndHostToken, schemeAndHostValue)
                .Replace(IdentifierToken, identifier);
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
            return PersistentCatalogueRecordFormat.Replace(IdentifierToken, identifier);
        }

        public string EncoreBibliographicData(string identifier)
        {
            return EncoreBibliographicDataFormat.Replace(IdentifierToken, identifier);
        }
        
        
        
    }
}