using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private const string DlcsEntryPointToken = "{dlcsEntryPoint}";
        private const string AssetIdentifierToken = "{assetIdentifier}";
        private const string RangeIdentifierToken = "{rangeIdentifier}";
        private const string AnnoIdentifierToken = "{annoIdentifier}";
        private const string VersionToken = "{version}";
        private const string FileExtensionToken = "{fileExt}";

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
        private const string CanvasSuppAnnotationPageFormat =     "/presentation/{identifier}/canvases/{assetIdentifier}/supplementing";
        private const string CanvasSuppAnnotationFormat =         "/presentation/{identifier}/canvases/{assetIdentifier}/supplementing/{annoIdentifier}";
        private const string CanvasClassifyingAnnotationFormat =  "/presentation/{identifier}/canvases/{assetIdentifier}/classifying/{annoIdentifier}";
        private const string RangeFormat =                        "/presentation/{identifier}/ranges/{rangeIdentifier}";

        private const string IIIFSearchAnnotationFormat =         "/annotations/{identifier}/{assetIdentifier}/{annoIdentifier}";
        
        // Always versioned - todo... bring version out as parameter? 
        // NB /line/ is reserved for text granularity - can be other granularities later.
        private const string CanvasOtherAnnotationPageFormat =    "/annotations/v{version}/{identifier}/{assetIdentifier}/line";
        private const string CanvasOtherAnnotationFormat =        "/annotations/v{version}/{identifier}/{assetIdentifier}/line/{annoIdentifier}";
        private const string ManifestAnnotationPageAllFormat =    "/annotations/v{version}/{identifier}/all/line";
        private const string ManifestAnnotationPageImagesFormat = "/annotations/v{version}/{identifier}/images"; // not line, obvs.

        // IIIF Content Search
        private const string IIIFContentSearch0Format = "/search/v0/{identifier}";
        // private const string IIIFAutoComplete0Format = "/search/autocomplete/v0/{identifier}";  // not used - use v1
        private const string IIIFContentSearch1Format = "/search/v1/{identifier}";
        private const string IIIFAutoComplete1Format = "/search/autocomplete/v1/{identifier}";
        private const string IIIFContentSearch2Format = "/search/v2/{identifier}";
        private const string IIIFAutoComplete2Format = "/search/autocomplete/v2/{identifier}";

        // Other resources
        private const string RawTextFormat =                      "/text/v1/{identifier}"; // v1 refers to Wellcome API
        private const string MetsAltoFormat =                     "/text/alto/{identifier}/{assetIdentifier}"; // not versioned
        private const string PosterImageFormat =                  "/thumb/{identifier}";
        private const string PdfThumbnailFormat =                 "/thumb/{identifier}"; // make these the same for now
        
        // TODO - rename to WorkPageFormat, once fully ported.
        private const string PersistentPlayerUriFormat = "https://wellcomecollection.org/works/{identifier}";
        private const string PersistentCatalogueRecordFormat = "https://search.wellcomelibrary.org/iii/encore/record/C__R{identifier}";
        private const string EncoreBibliographicDataFormat = "https://search.wellcomelibrary.org/iii/queryapi/collection/bib/{identifier}?profiles=b(full)i(brief)&amp;format=xml";
        
        // TODO: these need to change to iiif.wellcomecollection.org/... once DLCS routes to it
        private const string DlcsPdfTemplate          = "{dlcsEntryPoint}/pdf/{identifier}";
        private const string DlcsThumbServiceTemplate = "{dlcsEntryPoint}/thumbs/{assetIdentifier}";
        private const string DlcsImageServiceTemplate = "{dlcsEntryPoint}/images/{assetIdentifier}";
        private const string DlcsVideoTemplate        = "{dlcsEntryPoint}/av/{assetIdentifier}/full/full/max/max/0/default.{fileExt}";
        private const string DlcsAudioTemplate        = "{dlcsEntryPoint}/av/{assetIdentifier}/full/max/default.{fileExt}";
        private const string DlcsFileTemplate         = "{dlcsEntryPoint}/file/{assetIdentifier}";

        public UriPatterns(
            IOptions<DdsOptions> ddsOptions,
            ICatalogue catalogue)
        {
            schemeAndHostValue = ddsOptions.Value.LinkedDataDomain;
            
            // TODO - it feels odd that to generate IIIF uri's you need a reference to ICatalogue
            // if you need a Catalogue URI reference ICatalogue directly?
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
        
        public string CanvasSupplementingAnnotationPage(string manifestIdentifier, string assetIdentifier)
        {
            return ManifestAndAssetIdentifiers(
                CanvasSuppAnnotationPageFormat, manifestIdentifier, assetIdentifier);
        }    
        public string CanvasSupplementingAnnotation(string manifestIdentifier, string assetIdentifier, string annoIdentifier)
        {
            return ManifestAndAssetAndAnnoIdentifiers(
                CanvasSuppAnnotationFormat, manifestIdentifier, assetIdentifier, annoIdentifier);
        }   
        
        public string CanvasClassifyingAnnotation(string manifestIdentifier, string assetIdentifier, string annoIdentifier)
        {
            return ManifestAndAssetAndAnnoIdentifiers(
                CanvasClassifyingAnnotationFormat, manifestIdentifier, assetIdentifier, annoIdentifier);
        }   
        
        public string CanvasOtherAnnotationPageWithVersion(string manifestIdentifier, string assetIdentifier, int version)
        {
            return ManifestAndAssetIdentifiersWithVersion(
                CanvasOtherAnnotationPageFormat, manifestIdentifier, assetIdentifier, version);
        }

        public string CanvasOtherAnnotationWithVersion(string manifestIdentifier, string assetIdentifier, string annoIdentifier, int version)
        {
            return ManifestAndAssetAndAnnoIdentifiersWithVersion(
                CanvasOtherAnnotationFormat, manifestIdentifier, assetIdentifier, annoIdentifier, version);
        }
        
        
        public string IIIFSearchAnnotation(string manifestIdentifier, string assetIdentifier, string annoIdentifier)
        {
            return ManifestAndAssetAndAnnoIdentifiers(
                    IIIFSearchAnnotationFormat, manifestIdentifier, assetIdentifier, annoIdentifier);
        }
        
        public string ManifestAnnotationPageAllWithVersion(string identifier, int version)
        {
            return ManifestIdentifierWithVersion(ManifestAnnotationPageAllFormat, identifier, version);
        }
        
        public string ManifestAnnotationPageImagesWithVersion(string identifier, int version)
        {
            return ManifestIdentifierWithVersion(ManifestAnnotationPageImagesFormat, identifier, version);
        }

        public string CollectionForAggregation()
        {
            return $"{schemeAndHostValue}{AggregationFormat}";
        }
        
        public string CollectionForAggregation(string aggregator)
        {
            return $"{schemeAndHostValue}{AggregationFormat}/{aggregator}";
        }
        
        public string CollectionForAggregation(string aggregator, string value)
        {
            return $"{schemeAndHostValue}{AggregationFormat}/{aggregator}/{value}";
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


        public string DlcsPdf(string dlcsEntryPoint, string identifier)
        {
            return DlcsPdfTemplate
                .Replace(DlcsEntryPointToken, dlcsEntryPoint)
                .Replace(IdentifierToken, identifier);
        }

        public string DlcsThumb(string dlcsEntryPoint, string assetIdentifier)
        {
            return DlcsIdentifier(DlcsThumbServiceTemplate, dlcsEntryPoint, assetIdentifier);
        }
        
        public string DlcsImageService(string dlcsEntryPoint, string assetIdentifier)
        {
            return DlcsIdentifier(DlcsImageServiceTemplate, dlcsEntryPoint, assetIdentifier);
        }

        public string DlcsVideo(string dlcsEntryPoint, string assetIdentifier, string fileExt)
        {
            return DlcsIdentifier(DlcsVideoTemplate, dlcsEntryPoint, assetIdentifier)
                .Replace(FileExtensionToken, fileExt);
        }

        public string DlcsAudio(string dlcsEntryPoint, string assetIdentifier, string fileExt)
        {
            return DlcsIdentifier(DlcsAudioTemplate, dlcsEntryPoint, assetIdentifier)
                .Replace(FileExtensionToken, fileExt);
        }
        
        public string DlcsFile(string dlcsEntryPoint, string assetIdentifier)
        {
            return DlcsIdentifier(DlcsFileTemplate, dlcsEntryPoint, assetIdentifier);
        }

        private string DlcsIdentifier(string template, string dlcsEntryPointToken, string assetIdentifier)
        {
            return template
                .Replace(DlcsEntryPointToken, dlcsEntryPointToken)
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
        public string IIIFContentSearchService1(string identifier)
        {
            return ManifestIdentifier(IIIFContentSearch1Format, identifier);
        }
        public string IIIFContentSearchService0(string identifier)
        {
            return ManifestIdentifier(IIIFContentSearch0Format, identifier);
        }

        public string IIIFAutoCompleteService2(string identifier)
        {
            return ManifestIdentifier(IIIFAutoComplete2Format, identifier);
        }
        public string IIIFAutoCompleteService1(string identifier)
        {
            return ManifestIdentifier(IIIFAutoComplete1Format, identifier);
        }

        private string ManifestIdentifier(string template, string identifier)
        {
            var path = template.Replace(IdentifierToken, identifier);
            return $"{schemeAndHostValue}{path}";
        }
        
        private string ManifestIdentifierWithVersion(string template, string identifier, int version)
        {
            return ManifestIdentifier(template, identifier)
                .Replace(VersionToken, version.ToString());
        }
        
        private string ManifestAndAssetIdentifiers(string template, string manifestIdentifier, string assetIdentifier)
        {
            return ManifestIdentifier(template, manifestIdentifier)
                .Replace(AssetIdentifierToken, assetIdentifier);
        }
        
        private string ManifestAndAssetIdentifiersWithVersion(string template, string manifestIdentifier, string assetIdentifier, int version)
        {
            return ManifestIdentifierWithVersion(template, manifestIdentifier, version)
                .Replace(AssetIdentifierToken, assetIdentifier);
        }
        
        private string ManifestAndAssetAndAnnoIdentifiers(string template, string manifestIdentifier, string assetIdentifier, string annoIdentifier)
        {
            return ManifestAndAssetIdentifiers(template, manifestIdentifier, assetIdentifier)
                .Replace(AnnoIdentifierToken, annoIdentifier);
        }
        
        private string ManifestAndAssetAndAnnoIdentifiersWithVersion(string template, string manifestIdentifier, string assetIdentifier, string annoIdentifier, int version)
        {
            return ManifestAndAssetIdentifiersWithVersion(template, manifestIdentifier, assetIdentifier, version)
                .Replace(AnnoIdentifierToken, annoIdentifier);
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
        
        public string PdfThumbnail(string manifestIdentifier)
        {
            return ManifestIdentifier(PdfThumbnailFormat, manifestIdentifier);
        }

        public string GetPath(string format, string manifestIdentifier,
            params (string Token, string Replacement)[] replacements)
        {
            var path = ManifestIdentifier(format, manifestIdentifier);

            foreach (var (token, value) in replacements ?? Enumerable.Empty<(string, string)>())
            {
                path = path.Replace(token, value);
            }

            return path;
        }

        
    }
}