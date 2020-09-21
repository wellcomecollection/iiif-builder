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
        //private const string SchemeAndHostToken = "{schemeAndHost}";
        private const string IdentifierToken = "{identifier}";
        private const string SpaceToken = "{space}";
        private const string AssetIdentifierToken = "{assetIdentifier}";
        private const string RangeIdentifierToken = "{rangeIdentifier}";
        private const string AnnoIdentifierToken = "{annoIdentifier}";
        
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
        
        // IIIF Presentation
        
        // Canonical - negotiable
        private const string ManifestFormat =                     "/presentation/{identifier}";
        private const string CanvasFormat =                       "/presentation/{identifier}/canvases/{assetIdentifier}";
        private const string AggregationFormat =                  "/presentation/collections";
        
        // Canonical - conceptual (not dereffed...)
        private const string CanvasPaintingAnnotationPageFormat = "/presentation/{identifier}/canvases/{assetIdentifier}/painting";
        private const string CanvasPaintingAnnotationFormat =     "/presentation/{identifier}/canvases/{assetIdentifier}/painting/anno";
        private const string RangeFormat =                        "/presentation/{identifier}/ranges/{rangeIdentifier}";
        
        // Always versioned - todo... bring version out as parameter? 
        // NB /line/ is reserved for text granularity - can be other granularities later.
        private const string CanvasOtherAnnotationPageFormat =    "/annotations/v3/{identifier}/{assetIdentifier}/line";
        private const string CanvasOtherAnnotationFormat =        "/annotations/v3/{identifier}/{assetIdentifier}/line/{annoIdentifer}";
        private const string ManifestAnnotationPageAllFormat =    "/annotations/v3/{identifier}/all/line";
        private const string ManifestAnnotationPageImagesFormat = "/annotations/v3/{identifier}/images"; // not line, obvs.
        
        // IIIF Content Search
        private const string IIIFContentSearch2Format =           "/search/v2/{identifier}";
        private const string IIIFAutoComplete2Format =            "/autocomplete/v2/{identifier}";
        
        // Other resources
        private const string RawTextFormat =                      "/text/v1/{identifier}"; // v1 refers to Wellcome API
        private const string MetsAltoFormat =                     "/text/alto/{identifier}/{assetIdentifier}"; // v1 refers to Wellcome API
        private const string PosterImageFormat =                  "/thumbs/{identifier}";
        
        // TODO - rename to WorkPageFormat, once fully ported.
        private const string PersistentPlayerUriFormat = "https://wellcomecollection.org/works/{identifier}";
        private const string PersistentCatalogueRecordFormat = "https://search.wellcomelibrary.org/iii/encore/record/C__R{identifier}";
        private const string EncoreBibliographicDataFormat = "https://search.wellcomelibrary.org/iii/queryapi/collection/bib/{identifier}?profiles=b(full)i(brief)&amp;format=xml";
        
        // TODO: these need to change to iiif.wellcomecollection.org/... once DLCS routes to it
        private const string DlcsPdfTemplate = "https://dlcs.io/pdf/wellcome/pdf/{space}/{identifier}";
        private const string DlcsThumbServiceTemplate = "https://dlcs.io/thumbs/wellcome/{space}/{assetIdentifier}";
        private const string DlcsImageServiceTemplate = "https://dlcs.io/iiif-img/wellcome/{space}/{assetIdentifier}";
        
        
        public UriPatterns(
            IOptions<DdsOptions> ddsOptions,
            ICatalogue catalogue)
        {
            schemeAndHostValue = ddsOptions.Value.LinkedDataDomain;
            this.catalogue = catalogue;
        }

        public string Manifest(string identifier)
        {
            return ManifestIdentifier(ManifestFormat, identifier);
        }

        public string CollectionForWork(string identifier)
        {
            // yes, these two are the same. Do we want to reserve separate usage?
            return Manifest(identifier);
        }
        
        
        public string Canvas(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasFormat, manifestIdentifier, assetIdentifier);
        }       
        
        public string CanvasPaintingAnnotationPage(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasPaintingAnnotationPageFormat, manifestIdentifier, assetIdentifier);
        }    
        public string CanvasPaintingAnnotation(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasPaintingAnnotationFormat, manifestIdentifier, assetIdentifier);
        }        
        
        public string CanvasOtherAnnotationPage(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasOtherAnnotationPageFormat, manifestIdentifier, assetIdentifier);
        }

        public string CanvasOtherAnnotation(string manifestIdentifier, string assetIdentifier, string annoIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasOtherAnnotationFormat, manifestIdentifier, assetIdentifier)
                .Replace(AnnoIdentifierToken, annoIdentifier);
        }

        public string ManifestAnnotationPageAll(string identifier)
        {
            return ManifestIdentifier(ManifestAnnotationPageAllFormat, identifier);
        }
        
        public string ManifestAnnotationPageImages(string identifier)
        {
            return ManifestIdentifier(ManifestAnnotationPageImagesFormat, identifier);
        }

        public string CollectionForAggregation()
        {
            return AggregationFormat;
        }
        
        public string CollectionForAggregation(string aggregator)
        {
            return $"{AggregationFormat}/{aggregator}";
        }
        
        public string CollectionForAggregation(string aggregator, string value)
        {
            return $"{AggregationFormat}/{aggregator}/{value}";
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


        public string DlcsPdf(int space, string identifier)
        {
            return DlcsPdfTemplate
                .Replace(SpaceToken, space.ToString())
                .Replace(IdentifierToken, identifier);
        }

        public string DlcsThumb(int space, string assetIdentifier)
        {
            return DlcsThumbServiceTemplate
                .Replace(SpaceToken, space.ToString())
                .Replace(AssetIdentifierToken, assetIdentifier);
        }
        
        public string DlcsImageService(int space, string assetIdentifier)
        {
            return DlcsImageServiceTemplate
                .Replace(SpaceToken, space.ToString())
                .Replace(AssetIdentifierToken, assetIdentifier);
        }

        public string RawText(string identifier)
        {
            return ManifestIdentifier(RawTextFormat, identifier);
        }
        
        public string MetsAlto(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(MetsAltoFormat, manifestIdentifier, assetIdentifier);
        }

        public string IIIFContentSearchService2(string identifier)
        {
            return ManifestIdentifier(IIIFContentSearch2Format, identifier);
        }

        public string IIIFAutoCompleteService2(string identifier)
        {
            return ManifestIdentifier(IIIFAutoComplete2Format, identifier);
        }

        private string ManifestIdentifier(string template, string identifier)
        {
            var path = template.Replace(IdentifierToken, identifier);
            return $"{schemeAndHostValue}{path}";
        }
        
        private string ManifestAndAssetIdentifiers(string template, string manifestIdentifier, string assetIdentifier)
        {
            return ManifestIdentifier(template, manifestIdentifier)
                .Replace(AssetIdentifierToken, assetIdentifier);
        }

        public string Range(string manifestIdentifier, string rangeIdentifier)
        {
            return ManifestIdentifier(RangeFormat, manifestIdentifier)
                .Replace(RangeIdentifierToken, rangeIdentifier);
        }

        public string PosterImage(string manifestIdentifier)
        {
            return ManifestIdentifier(PosterImageFormat, manifestIdentifier);
        }
    }
}