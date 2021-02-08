using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using IIIF;
using IIIF.LegacyInclusions;
using IIIF.Presentation;
using IIIF.Presentation.Annotation;
using IIIF.Presentation.Constants;
using IIIF.Presentation.Content;
using IIIF.Presentation.Strings;
using IIIF.Search.V1;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using Wellcome.Dds.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.Search;
using Wellcome.Dds.WordsAndPictures.SimpleAltoServices;
using Annotation = IIIF.Presentation.Annotation.Annotation;
using AnnotationPage = Wellcome.Dds.WordsAndPictures.SimpleAltoServices.AnnotationPage;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private readonly IDds dds;
        private readonly IMetsRepository metsRepository;
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DdsOptions ddsOptions;
        private readonly UriPatterns uriPatterns;
        private readonly IIIFBuilderParts build;
        
        public IIIFBuilder(
            IDds dds,
            IMetsRepository metsRepository,
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DdsOptions> ddsOptions,
            IOptions<DlcsOptions> dlcsOptions,
            UriPatterns uriPatterns)
        {
            this.dds = dds;
            this.metsRepository = metsRepository;
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.ddsOptions = ddsOptions.Value;
            this.uriPatterns = uriPatterns;
            build = new IIIFBuilderParts(
                uriPatterns,
                dlcsOptions.Value.CustomerDefaultSpace,
                ddsOptions.Value.ReferenceV0SearchService);
        }

        public async Task<MultipleBuildResult> BuildAllManifestations(string bNumber, Work work = null)
        {
            var state = new State();
            var buildResults = new MultipleBuildResult {Identifier = bNumber};
            var ddsId = new DdsIdentifier(bNumber);
            if (ddsId.IdentifierType != IdentifierType.BNumber)
            {
                // we could throw an exception - do we actually care?
                // just process it. 
            }
            bNumber = ddsId.BNumber;
            var manifestationId = "start";
            try
            {
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.BNumber);
                var resource = await dashboardRepository.GetDigitisedResource(bNumber);
                // This is a bnumber, so can't be part of anything.
                buildResults.Add(BuildInternal(work, resource, null, manifestationMetadata, state));
                if (resource is IDigitisedCollection parentCollection)
                {
                    // This will need some special treatment to build Chemist and Druggist in the
                    // Collection structure required for date-based navigation, and handle the two levels.
                    // Come back to that once we have the basics working.
                    // C&D needs a special build process.
                    await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(bNumber))
                    {
                        var manifestation = manifestationInContext.Manifestation;
                        manifestationId = manifestation.Id;
                        var digitisedManifestation = await dashboardRepository.GetDigitisedResource(manifestationId);
                        buildResults.Add(BuildInternal(work, digitisedManifestation, parentCollection, manifestationMetadata, state));
                    }
                }
            }
            catch (Exception e)
            {
                buildResults.Message = $"Failed at {manifestationId}, {e.Message}";
                buildResults.Outcome = BuildOutcome.Failure;
                return buildResults;
            }

            CheckAndProcessState(buildResults, state);
            
            // Now the buildResults should have what actually needs to be persisted.
            // Only now do we convert them to IIIF2
            // TODO - make this optional - dashboard PeekController shouldn't trigger this
            foreach (var buildResult in buildResults)
            {
                // now build the Presentation 2 version from the Presentation 3 version
                var iiifPresentation2Resource = MakePresentation2Resource(buildResult.IIIF3Resource);
                buildResult.IIIF2Resource = iiifPresentation2Resource;
                buildResult.IIIF2Key = $"v2/{buildResult.Id}";
            }
            
            return buildResults;
        }

        private void CheckAndProcessState(MultipleBuildResult buildResults, State state)
        {
            if (!state.HasState)
            {
                return;
            }

            if (state.MultiCopyState != null)
            {
                MultiCopyState.ProcessState(buildResults, state);
            } 
            else if (state.AVState != null)
            {
                build.ProcessAVState(buildResults, state);
            }
            else if (state.ChemistAndDruggistState != null)
            {
                // Fear me...
                ChemistAndDruggistState.ProcessState(buildResults, state);
            }
        }

        public async Task<MultipleBuildResult> Build(string identifier, Work work = null)
        {
            // only this identifier, not all for the b number.
            var buildResults = new MultipleBuildResult();
            try
            {
                var ddsId = new DdsIdentifier(identifier);
                var digitisedResource = await dashboardRepository.GetDigitisedResource(identifier);
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.BNumber);
                IDigitisedCollection partOf = null;
                if (ddsId.IdentifierType != IdentifierType.BNumber)
                {
                    // this identifier has a parent, which we will need to build the resource properly
                    // this parent property smells... need to do some work to make sure this is always an identical
                    // result to BuildAndSaveAllManifestations
                    partOf = await dashboardRepository.GetDigitisedResource(ddsId.Parent) as IDigitisedCollection;
                }

                if (digitisedResource is IDigitisedCollection collection)
                {
                    if (collection.Manifestations.Any(m => m.MetsManifestation.IsMultiPart()))
                    {
                        buildResults.Add(new BuildResult(identifier) {RequiresMultipleBuild = true});
                        return buildResults;
                    }
                }
                
                // We can't supply state for a single build.
                // We should throw an exception. 
                // The dash preview can choose to handle this, for multicopy and AV, but not for C&D.
                // (dash preview can go back and rebuild all, then pick out the one asked for, 
                // or redirect to a single AV manifest).
                buildResults.Add(BuildInternal(work, digitisedResource, partOf, manifestationMetadata, null));
            }
            catch (Exception e)
            {
                buildResults.Message = e.Message;
                buildResults.Outcome = BuildOutcome.Failure;
            }
            return buildResults;
        }
        

        private BuildResult BuildInternal(Work work,
            IDigitisedResource digitisedResource, IDigitisedCollection partOf,
            ManifestationMetadata manifestationMetadata, State state)
        {
            var result = new BuildResult(digitisedResource.Identifier);
            try
            {
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(
                    digitisedResource, partOf, work, manifestationMetadata, state);
                result.IIIF3Resource = iiifPresentation3Resource;
                result.IIIF3Key = $"v3/{digitisedResource.Identifier}";
                result.Outcome = BuildOutcome.Success;
            }
            catch (IIIFBuildStateException bex)
            {
                result.RequiresMultipleBuild = true;
                result.Message = bex.Message;
                result.Outcome = BuildOutcome.Failure;
            }
            catch (Exception e)
            {
                result.Message = e.Message;
                result.Outcome = BuildOutcome.Failure;
            }
            return result;
        }


        /// <summary>
        /// </summary>
        /// <param name="digitisedResource"></param>
        /// <param name="partOf"></param>
        /// <param name="work"></param>
        /// <param name="manifestationMetadata"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        private StructureBase MakePresentation3Resource(
            IDigitisedResource digitisedResource,
            IDigitisedCollection partOf,
            Work work,
            ManifestationMetadata manifestationMetadata,
            State state)
        {
            switch (digitisedResource)
            {
                case IDigitisedCollection digitisedCollection:
                    var collection = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(digitisedCollection.Identifier)
                    };
                    AddCommonMetadata(collection, work, manifestationMetadata);
                    BuildCollection(collection, digitisedCollection, work, manifestationMetadata, state);
                    return collection;
                case IDigitisedManifestation digitisedManifestation:
                    var manifest = new Manifest
                    {
                        Id = uriPatterns.Manifest(digitisedManifestation.Identifier)
                    };
                    if (partOf != null)
                    {
                        manifest.PartOf = new List<ResourceBase>
                        {
                            new Collection
                            {
                                Id = uriPatterns.CollectionForWork(partOf.Identifier),
                                Label = Lang.Map(work.Title),
                                Behavior = new List<string>{Behavior.MultiPart}
                            }
                        };
                    }
                    AddCommonMetadata(manifest, work, manifestationMetadata);
                    BuildManifest(manifest, digitisedManifestation, manifestationMetadata, state);
                    return manifest;
            }
            throw new NotSupportedException("Unhandled type of Digitised Resource");
        }

        private void BuildCollection(Collection collection,
            IDigitisedCollection digitisedCollection,
            Work work,
            ManifestationMetadata manifestationMetadata,
            State state)
        {
            // TODO - use of Labels.
            // The work label should be preferred over the METS label,
            // but sometimes there is structural (volume) labelling that the catalogue API won't know about.
            collection.Items = new List<ICollectionItem>();
            collection.Behavior ??= new List<string>();
            collection.Behavior.Add(Behavior.MultiPart);
            if (digitisedCollection.Collections.HasItems())
            {
                foreach (var coll in digitisedCollection.Collections)
                {
                    collection.Items.Add(new Collection
                    {
                        Id = uriPatterns.CollectionForWork(coll.Identifier),
                        Label = Lang.Map(coll.MetsCollection.Label)
                    });
                }
            }

            if (digitisedCollection.Manifestations.HasItems())
            {
                int counter = 1;
                foreach (var mf in digitisedCollection.Manifestations)
                {
                    var type = mf.MetsManifestation.Type;
                    if (type == "Video" || type == "Audio" || type == "Transcript") // anything else?
                    {
                        if (state == null)
                        {
                            // This won't be true once we have handled the new AV model 
                            // https://github.com/wellcomecollection/platform/issues/4788
                            // but for now, it is.
                            throw new IIIFBuildStateException("State is required to build AV resources");
                        }
                        state.AVState ??= new AVState();
                        state.AVState.MultipleManifestationMembers.Add(new MultipleManifestationMember
                        {
                            Id = mf.MetsManifestation.Id,
                            Type = type
                        });
                    }
                    var order = mf.MetsManifestation.Order;
                    if (!order.HasValue || order < 1)
                    {
                        order = counter;
                    }

                    collection.Items.Add(new Manifest
                    {
                        Id = uriPatterns.Manifest(mf.Identifier),
                        Label = new LanguageMap
                        {
                            ["en"] = new List<string>
                            {
                                $"Volume {order}",
                                work.Title
                            }
                        },
                        Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(mf.Identifier)
                    });
                    counter++;
                }
            }
        }
        



        private void BuildManifest(
            Manifest manifest, 
            IDigitisedManifestation digitisedManifestation,
            ManifestationMetadata manifestationMetadata,
            State state)
        {
            manifest.Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(digitisedManifestation.Identifier);
            build.RequiredStatement(manifest, digitisedManifestation, manifestationMetadata);
            build.Rights(manifest, digitisedManifestation);
            build.PagedBehavior(manifest, digitisedManifestation);
            build.ViewingDirection(manifest, digitisedManifestation); // do we do this?
            build.Rendering(manifest, digitisedManifestation);
            build.SearchServices(manifest, digitisedManifestation);
            build.Canvases(manifest, digitisedManifestation, state);
            // do this next... both the next two use the manifestStructureHelper
            build.Structures(manifest, digitisedManifestation); // ranges
            build.ImprovePagingSequence(manifest);
            build.CheckForCopyAndVolumeStructure(digitisedManifestation, state);
            build.ManifestLevelAnnotations(manifest, digitisedManifestation);
        }
        
        
        /// <summary>
        /// Metadata, providers etc that are found on Work-level collections as
        /// well as manifests
        /// </summary>
        /// <param name="iiifResource"></param>
        /// <param name="work"></param>
        /// <param name="manifestationMetadata"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void AddCommonMetadata(
            StructureBase iiifResource, Work work,
            ManifestationMetadata manifestationMetadata)
        {
            iiifResource.EnsurePresentation3Context();
            iiifResource.Label = Lang.Map(work.Title);
            build.SeeAlso(iiifResource, work);
            iiifResource.AddWellcomeProvider(ddsOptions.LinkedDataDomain);
            iiifResource.AddOtherProvider(manifestationMetadata, ddsOptions.LinkedDataDomain);
            build.Aggregations(iiifResource, manifestationMetadata);
            build.Summary(iiifResource, work);
            build.HomePage(iiifResource, work);
            build.Metadata(iiifResource, work);
            build.ArchiveCollectionStructure(iiifResource, work);
        }
     
        /// <summary>
        /// Convert the IIIF v3 Manifest into its equivalent v2 manifest
        /// </summary>
        /// <returns></returns>
        private StructureBase MakePresentation2Resource(StructureBase iiifPresentation3Resource)
        {
            // TODO - this is obviously a placeholder!
            var p2Version = new Manifest
            {
                Label = Lang.Map("[IIIF 2.1 version of] " + iiifPresentation3Resource.Label)
            };
            return p2Version;
        }

        public AltoAnnotationBuildResult BuildW3CAndOaAnnotations(IManifestation manifestation, AnnotationPageList annotationPages)
        {
            // This is in the wrong place. We're making S3 keys out of anno IDs (URLs), but
            // _where_ is the right place to do this?
            
            // loop through annotationPages
            // build annos for each page
            // emit a single page and key per page
            // append the annos to the all list
            // append just the images to the image list
            // See ecosystem IIIFConverter in ecosystem, line 1610
            var result = new AltoAnnotationBuildResult(manifestation)
            {
                // W3C
                AllContentAnnotations = new()
                {
                    Id = uriPatterns.ManifestAnnotationPageAllWithVersion(manifestation.Id, 3),
                    Items = new List<IAnnotation>()
                },
                ImageAnnotations = new()
                {
                    Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(manifestation.Id, 3),
                    Items = new List<IAnnotation>()
                },
                PageAnnotations = new IIIF.Presentation.Annotation.AnnotationPage[annotationPages.Count],
                // OA
                OpenAnnotationAllContentAnnotations = new ()
                {
                    Id = uriPatterns.ManifestAnnotationPageAllWithVersion(manifestation.Id, 2),
                    Resources = new List<IAnnotation>()
                },
                OpenAnnotationImageAnnotations = new ()
                {
                    Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(manifestation.Id, 2),
                    Resources = new List<IAnnotation>()
                }
            };
            result.AllContentAnnotations.EnsurePresentation3Context();
            result.ImageAnnotations.EnsurePresentation3Context();
            result.OpenAnnotationAllContentAnnotations.EnsurePresentation2Context();
            result.OpenAnnotationImageAnnotations.EnsurePresentation2Context();
            for (var i = 0; i < annotationPages.Count; i++)
            {
                var altoPage = annotationPages[i];
                var w3CPage = new IIIF.Presentation.Annotation.AnnotationPage
                {
                    Id = uriPatterns.CanvasOtherAnnotationPageWithVersion(manifestation.Id, altoPage.AssetIdentifier, 3),
                    Items = new List<IAnnotation>()
                };
                w3CPage.EnsurePresentation3Context();
                string canvasId = uriPatterns.Canvas(manifestation.Id, altoPage.AssetIdentifier);

                // Do the W3C conversions
                var w3CTextLines = altoPage.TextLines
                    .Select((tl, lineIndex) => GetW3CTextLineAnnotation(altoPage, tl, lineIndex, canvasId))
                    .ToList();
                var w3CIllustrations = altoPage.Illustrations
                    .Select((il, index) => GetW3CIllustrationAnnotation(altoPage, il, index, canvasId))
                    .ToList();
                var w3CComposedBlocks = altoPage.ComposedBlocks
                    .Select((il, index) => GetW3CIllustrationAnnotation(altoPage, il, index, canvasId))
                    .ToList();
                var allW3CPageAnnotations = w3CTextLines.Concat(w3CIllustrations).Concat(w3CComposedBlocks).ToArray();

                if (allW3CPageAnnotations.FirstOrDefault()?.Target is Canvas firstAnnoCanvas)
                {
                    // add a partOf to the first anno for this page, to associate the canvas with the manifest. Nice!
                    firstAnnoCanvas.PartOf = new List<ResourceBase>
                    {
                        new Manifest {Id = uriPatterns.Manifest(manifestation.Id)}
                    };
                }
                result.AllContentAnnotations.Items.AddRange(allW3CPageAnnotations);
                result.ImageAnnotations.Items.AddRange(w3CIllustrations);
                result.ImageAnnotations.Items.AddRange(w3CComposedBlocks);
                w3CPage.Items.AddRange(allW3CPageAnnotations);
                result.PageAnnotations[i] = w3CPage;
                
                
                // Now do the OA Conversions:
                var oATextLines = altoPage.TextLines
                    .Select((tl, lineIndex) => GetOATextLineAnnotation(altoPage, tl, lineIndex, canvasId))
                    .ToList();
                var oAIllustrations = altoPage.Illustrations
                    .Select((il, index) => GetOAIllustrationAnnotation(altoPage, il, index, canvasId))
                    .ToList();
                var oAComposedBlocks = altoPage.ComposedBlocks
                    .Select((il, index) => GetOAIllustrationAnnotation(altoPage, il, index, canvasId))
                    .ToList();
                var allOAPageAnnotations = oATextLines.Concat(oAIllustrations).Concat(oAComposedBlocks).ToArray();

                result.OpenAnnotationAllContentAnnotations.Resources.AddRange(allOAPageAnnotations);
                result.OpenAnnotationImageAnnotations.Resources.AddRange(oAIllustrations);
                result.OpenAnnotationImageAnnotations.Resources.AddRange(oAComposedBlocks);
            }
            return result;
        }


        private Annotation GetW3CTextLineAnnotation(
            AnnotationPage altoPage, TextLine tl, int lineIndex, string canvasId)
        {
            return new SupplementingDocumentAnnotation
            {
                Id = uriPatterns.CanvasSupplementingAnnotation(
                    altoPage.ManifestationIdentifier, altoPage.AssetIdentifier, $"t{lineIndex}"),
                Target = new Canvas { Id = $"{canvasId}#xywh={tl.X},{tl.Y},{tl.Width},{tl.Height}" },
                Body = new TextualBody(tl.Text) 
                // we could use the overload TextualBody(tl.Text, "text/plain"), but verbose.
            };
        }
        private Annotation GetW3CIllustrationAnnotation(
            AnnotationPage altoPage, Illustration il, int index, string canvasId)
        {
            return new TypeClassifyingAnnotation
            {
                Id = uriPatterns.CanvasClassifyingAnnotation(
                    altoPage.ManifestationIdentifier, altoPage.AssetIdentifier, $"i{index}"),
                Target = new Canvas { Id = $"{canvasId}#xywh={il.X},{il.Y},{il.Width},{il.Height}" },
                Body = new ClassifyingBody("Image")
                {
                    Label = Lang.Map(il.Type) // https://github.com/w3c/web-annotation/issues/437
                }
            };
        }

        private IIIF.LegacyInclusions.Annotation GetOATextLineAnnotation(
            AnnotationPage altoPage, TextLine tl, int lineIndex, string canvasId)
        {
            return new()
            {
                Id = uriPatterns.CanvasSupplementingAnnotation(
                    altoPage.ManifestationIdentifier, altoPage.AssetIdentifier, $"t{lineIndex}"),
                On = $"{canvasId}#xywh={tl.X},{tl.Y},{tl.Width},{tl.Height}",
                Motivation = "sc:painting",
                Resource = new ContentAsTextAnnotationResource
                {
                    Chars = tl.Text
                }
            };
        }

        private IIIF.LegacyInclusions.Annotation GetOAIllustrationAnnotation(
            AnnotationPage altoPage, Illustration il, int index, string canvasId)
        {
            return new IIIF.LegacyInclusions.Annotation
            {
                Id = uriPatterns.CanvasClassifyingAnnotation(
                    altoPage.ManifestationIdentifier, altoPage.AssetIdentifier, $"i{index}"),
                On = $"{canvasId}#xywh={il.X},{il.Y},{il.Width},{il.Height}",
                Motivation = "oa:classifying",
                Resource = new IllustrationAnnotationResource
                {
                    Id = "dctypes:Image",
                    Label = new MetaDataValue(il.Type)
                }
            };
        }
        

        public string Serialise(JsonLdBase iiifResource)
        {
            return JsonConvert.SerializeObject(iiifResource, GetJsFriendlySettings());
        }

        public TermList BuildTermListV1(string manifestationIdentifier, string q, string[] suggestions)
        {
            var searchUri = uriPatterns.IIIFContentSearchService1(manifestationIdentifier);
            return new TermList
            {
                Context = SearchService1.Search1Context,
                Id = uriPatterns.IIIFAutoCompleteService1(manifestationIdentifier) + "?q=" + q,
                Terms = suggestions.Select(suggestion => new Term
                {
                        Match = suggestion,
                        Search = searchUri + "?q=" + q
                }).ToArray()
            };
        }

        /// <summary>
        /// // see https://github.com/wellcomecollection/platform/issues/4740#issuecomment-775035270
        ///
        /// The V0 version is page-centric, it uses "Simple Player" results.
        /// The V1 version allows for hits to span pages, or have more than one hit per page.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="results"></param>
        /// <param name="manifestationIdentifier"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public SearchResultAnnotationList BuildSearchResultsV0(
            Text text,
            IEnumerable<SearchResult> results,
            string manifestationIdentifier,
            string query)
        {
            var resultsList = results.ToList();

            // this is the simple annotation list:
            var resources = new List<SearchResultAnnotation>();

            // and these are the decorations that turn it into search results.
            // a hit might map to more than one annotation, if the result spans lines.
            var hits = new List<Hit>();
            
            foreach(SearchResult sr in resultsList)
            {
                var hit = new Hit();
                var hitAnnos = new List<string>();

                // we need some temporary jiggery-pokery to massage our old format before and afters (which belong to each Rect)
                string firstBefore = null;
                string lastAfter = null;
                string match = "";
                foreach(Rect rect in sr.Rects)
                {
                    var assetIdentifier = text.Images[sr.Index].ImageIdentifier;
                    var annoIdentifier = $"h{rect.Hit}r{rect.X},{rect.Y},{rect.W},{rect.H}";
                    var canvasId = uriPatterns.Canvas(manifestationIdentifier, assetIdentifier);
                    var anno = new SearchResultAnnotation
                    {
                        Id = uriPatterns.IIIFSearchAnnotation(manifestationIdentifier, assetIdentifier, annoIdentifier),
                        On = $"{canvasId}#xywh={rect.X},{rect.Y},{rect.W},{rect.H}",
                        Resource = new SearchResultAnnotationResource { Chars = rect.Word }
                    };
                    if(firstBefore.IsNullOrWhiteSpace())
                    {
                        firstBefore = rect.Before;
                    }
                    // This is very implementation-specific
                    match += (match.HasText() ? " " : "") + rect.Word;
                    lastAfter = rect.After;
                    hitAnnos.Add(anno.Id);
                    resources.Add(anno);
                }
                hit.Before = firstBefore;
                hit.After = lastAfter;
                hit.Annotations = hitAnnos.ToArray();
                //if(hit.Annotations.Length > 1)
                //{
                hit.Match = match;
                //}
                hits.Add(hit);
            }
            
            return new SearchResultAnnotationList
            {
                Id = uriPatterns.IIIFContentSearchService0(manifestationIdentifier) + "?q=" + query,
                Context = SearchService1.Search1Context,
                Resources = new List<IAnnotation>(resources),
                Hits = hits.ToArray(),
                Within = new SearchResultsLayer { Total = resources.Count }
            };
        }

        /// <summary>
        /// Build IIIF Hits and Annotation directly, allowing for more complex behaviour
        /// https://github.com/wellcomecollection/platform/issues/4740#issuecomment-775035270
        /// </summary>
        /// <param name="text"></param>
        /// <param name="manifestationIdentifier"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public SearchResultAnnotationList BuildSearchResultsV1(Text text, string manifestationIdentifier, string query)
        {
            var resources = new List<SearchResultAnnotation>();
            List<ResultRect> resultRects;
            if (query.HasText())
            {
                resultRects = text.Search(query);
            }
            else
            {
                resultRects = new List<ResultRect>(0);
            }

            // number of rects >= number of hits
            var hits = new List<Hit>();
            var hitAnnotations = new List<string>();
            Hit currentHit = null;
            int currentHitIndex = -1;
            string after = null;
            
            foreach(var resultRect in resultRects)
            {
                if (currentHitIndex != resultRect.Hit)
                {
                    if (currentHit != null)
                    {
                        // Finish off the previous hit - we are now in another hit
                        currentHit.After = after;
                        currentHit.Annotations = hitAnnotations.ToArray();
                        hits.Add(currentHit);
                    }
                    
                    // start the new Hit
                    currentHit = new Hit
                    {
                        Before = resultRect.Before,
                        Match = ""
                    };
                    currentHitIndex = resultRect.Hit;
                    hitAnnotations = new List<string>();
                }

                var assetIdentifier = text.Images[resultRect.Idx].ImageIdentifier;
                var annoIdentifier = $"h{resultRect.Hit}r{resultRect.X},{resultRect.Y},{resultRect.W},{resultRect.H}";
                var canvasId = uriPatterns.Canvas(manifestationIdentifier, assetIdentifier);
                var anno = new SearchResultAnnotation
                {
                    Id = uriPatterns.IIIFSearchAnnotation(manifestationIdentifier, assetIdentifier, annoIdentifier),
                    On = $"{canvasId}#xywh={resultRect.X},{resultRect.Y},{resultRect.W},{resultRect.H}",
                    Resource = new SearchResultAnnotationResource { Chars = resultRect.ContentRaw }
                };
                currentHit.Match += (currentHit.Match.HasText() ? " " : "") + resultRect.ContentRaw;
                after = resultRect.After;
                hitAnnotations.Add(anno.Id);
                resources.Add(anno);
            }

            // Finish off the LAST hit - as long as we have some results
            if (currentHit != null)
            {
                currentHit.After = after;
                currentHit.Annotations = hitAnnotations.ToArray();
                hits.Add(currentHit);
            }

            return new SearchResultAnnotationList
            {
                Id = uriPatterns.IIIFContentSearchService0(manifestationIdentifier) + "?q=" + query,
                Context = SearchService1.Search1Context,
                Resources = new List<IAnnotation>(resources),
                Hits = hits.ToArray(),
                Within = new SearchResultsLayer { Total = resources.Count }
            };
        }

        private static JsonSerializerSettings GetJsFriendlySettings()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new PrettyIIIFContractResolver(),
                Formatting = Formatting.Indented
            };
            return settings;
        }
        
        

    }
}