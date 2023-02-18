using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using IIIF.Presentation;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using Wellcome.Dds.Repositories.Presentation.V2;
using Wellcome.Dds.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.Search;
using Wellcome.Dds.WordsAndPictures.SimpleAltoServices;
using Annotation = IIIF.Presentation.V3.Annotation.Annotation;
using AnnotationPage = Wellcome.Dds.WordsAndPictures.SimpleAltoServices.AnnotationPage;
using Collection = IIIF.Presentation.V3.Collection;
using Manifest = IIIF.Presentation.V3.Manifest;
using Canvas = IIIF.Presentation.V3.Canvas;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private const string DcTypesStillImage = "dctypes:StillImage";
        private readonly IDds dds;
        private readonly IMetsRepository metsRepository;
        private readonly IDigitalObjectRepository digitalObjectRepository;
        private readonly ICatalogue catalogue;
        private readonly DdsOptions ddsOptions;
        private readonly UriPatterns uriPatterns;
        private readonly ILogger<IIIFBuilder> logger;
        private readonly IIIFBuilderParts build;
        private readonly PresentationConverter presentation2Converter;

        public IIIFBuilder(
            IDds dds,
            IMetsRepository metsRepository,
            IDigitalObjectRepository digitalObjectRepository,
            ICatalogue catalogue,
            IOptions<DdsOptions> ddsOptions,
            IOptions<DlcsOptions> dlcsOptions,
            UriPatterns uriPatterns,
            ILogger<IIIFBuilder> logger)
        {
            this.dds = dds;
            this.metsRepository = metsRepository;
            this.digitalObjectRepository = digitalObjectRepository;
            this.catalogue = catalogue;
            this.ddsOptions = ddsOptions.Value;
            this.uriPatterns = uriPatterns;
            this.logger = logger;

            if (dlcsOptions.Value.ResourceEntryPoint.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Resource Entry Point not specified in DDS Options");
            }
            build = new IIIFBuilderParts(
                uriPatterns,
                dlcsOptions.Value.ResourceEntryPoint,
                ddsOptions.Value.ReferenceV0SearchService,
                ddsOptions.Value.IncludeExtraAccessConditionsInManifest.SplitByDelimiterIntoArray(','),
                dlcsOptions.Value.SupportsDeliveryChannels);
            presentation2Converter = new PresentationConverter(uriPatterns, logger);
        }

        public async Task<MultipleBuildResult> BuildAllManifestations(DdsIdentifier ddsId, Work? work = null)
        {
            var state = new State();
            if (ddsId.PackageIdentifier == KnownIdentifiers.ChemistAndDruggist)
            {
                state.ChemistAndDruggistState = new ChemistAndDruggistState(uriPatterns);
            }
            var buildResults = new MultipleBuildResult(ddsId.PackageIdentifier);
            DdsIdentifier manifestationId = "start";
            try
            {
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.PackageIdentifier);
                if (work == null)
                {
                    throw new InvalidOperationException("Can't build a Manifest without a Work from the Catalogue API");
                }
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.PackageIdentifier);
                logger.LogInformation("Build all Manifestations getting Mets Resource for {identifier}", ddsId);
                var resource = await metsRepository.GetAsync(ddsId.PackageIdentifier);
                if (resource == null)
                {
                    throw new InvalidOperationException("Can't build a Manifest without a Digital object from METS");
                }
                // This is a bnumber or born digital archive, so can't be part of any multiple manifestation.
                buildResults.Add(BuildInternal(work, resource, null, manifestationMetadata, state));
                if (resource is ICollection parentCollection)
                {
                    await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(ddsId.PackageIdentifier))
                    {
                        var manifestation = manifestationInContext.Manifestation;
                        manifestationId = manifestation.Identifier!;
                        logger.LogInformation("Build all Manifestations looping through manifestations: {identifier}", manifestationId);
                        var metsManifestation = await metsRepository.GetAsync(manifestationId);
                        logger.LogInformation("Will now build " + metsManifestation!.Identifier!);
                        buildResults.Add(BuildInternal(work, metsManifestation, parentCollection, manifestationMetadata, state));
                    }
                }
            }
            catch (Exception e)
            {
                buildResults.Message = $"Failed at {manifestationId}, {e.Message}";
                buildResults.Outcome = BuildOutcome.Failure;
                return buildResults;
            }

            await CheckAndProcessState(buildResults, state);
            return buildResults;
        }

        public MultipleBuildResult BuildLegacyManifestations(DdsIdentifier ddsId, IEnumerable<BuildResult> buildResults)
            => presentation2Converter.ConvertAll(ddsId.PackageIdentifier, buildResults);

        private async Task CheckAndProcessState(MultipleBuildResult buildResults, State state)
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
                // Don't fear me...
                await PopulateChemistAndDruggistState(state.ChemistAndDruggistState);
                state.ChemistAndDruggistState.ProcessState(buildResults);
            }
            
            if (state.RightsState != null)
            {
                RightsState.ProcessState(buildResults);
            }
        }

        private async Task PopulateChemistAndDruggistState(ChemistAndDruggistState state)
        {
            await foreach (var mic in metsRepository.GetAllManifestationsInContext(KnownIdentifiers.ChemistAndDruggist))
            {
                var volume = state.Volumes.SingleOrDefault(v => v.Identifier == mic.VolumeIdentifier);
                if (volume == null)
                {
                    volume = new ChemistAndDruggistVolume(mic.VolumeIdentifier);
                    state.Volumes.Add(volume);
                    var metsVolume = await metsRepository.GetAsync(mic.VolumeIdentifier!) as ICollection;
                    // populate volume fields
                    volume.Volume = metsVolume!.SectionMetadata!.Number;
                    volume.DisplayDate = metsVolume.SectionMetadata.DisplayDate;
                    volume.NavDate = state.GetNavDate(volume.DisplayDate);
                    volume.Label = metsVolume.SectionMetadata.Title;
                }
                logger.LogInformation($"Issue {mic.IssueIdentifier}, Volume {mic.VolumeIdentifier}");
                var issue = new ChemistAndDruggistIssue(mic.IssueIdentifier);
                volume.Issues.Add(issue);
                var metsIssue = mic.Manifestation;
                var mods = metsIssue.SectionMetadata;
                if (mods == null)
                {
                    throw new InvalidOperationException("No MODS data for C&D issue");
                }
                // populate issue fields
                issue.Title = mods.Title; // like "2293"
                issue.DisplayDate = mods.DisplayDate;
                issue.NavDate = state.GetNavDate(issue.DisplayDate);
                issue.Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(issue.NavDate.Month);
                issue.MonthNum = issue.NavDate.Month;
                issue.Year = issue.NavDate.Year;
                issue.Volume = volume.Volume;
                issue.PartOrder = mods.PartOrder;
                issue.Number = mods.Number;
                issue.Label = issue.DisplayDate;
                if (issue.Title.HasText() && issue.Title.Trim() != "-")
                {
                    issue.Label += $" (issue {issue.Title})";
                }
            }
        }

        public async Task<MultipleBuildResult> Build(DdsIdentifier ddsId, Work? work = null)
        {
            // only this identifier, not all for the b number.
            var buildResults = new MultipleBuildResult(ddsId);
            try
            {
                logger.LogInformation($"Build a single manifestation {ddsId}", ddsId);
                var metsResource = await metsRepository.GetAsync(ddsId);
                if (metsResource == null)
                {
                    throw new InvalidOperationException("Can't build a Manifest without a Digital object from METS");
                }
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.PackageIdentifier);
                if (work == null)
                {
                    throw new InvalidOperationException("Can't build a Manifest without a Work from the Catalogue API");
                }
                
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.PackageIdentifier);
                ICollection? partOf = null;
                if (ddsId.IdentifierType is IdentifierType.Volume or IdentifierType.BNumberAndSequenceIndex)
                {
                    logger.LogInformation("Getting parent METS resource {identifier}", ddsId.PackageIdentifier);
                    partOf = await metsRepository.GetAsync(ddsId.PackageIdentifier) as ICollection;
                }

                if (metsResource is ICollection collection)
                {
                    if (collection.Manifestations!.Any(m => m.IsMultiPart()))
                    {
                        buildResults.Add(new BuildResult(ddsId, Version.V3) {RequiresMultipleBuild = true});
                        return buildResults;
                    }
                }
                
                // We can't supply state for a single build.
                // We should throw an exception. 
                // The dash preview can choose to handle this, for multicopy and AV, but not for C&D.
                // (dash preview can go back and rebuild all, then pick out the one asked for, 
                // or redirect to a single AV manifest).
                buildResults.Add(BuildInternal(work, metsResource, partOf, manifestationMetadata, null));
            }
            catch (Exception e)
            {
                buildResults.Message = e.Message;
                buildResults.Outcome = BuildOutcome.Failure;
            }
            return buildResults;
        }
        

        private BuildResult BuildInternal(Work work, IMetsResource metsResource, 
            ICollection? partOfCollection,
            ManifestationMetadata manifestationMetadata, State? state)
        {
            var result = new BuildResult(metsResource.Identifier!, Version.V3);
            try
            {
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(
                    metsResource, partOfCollection, work, manifestationMetadata, state, result);
                result.IIIFResource = iiifPresentation3Resource;
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
        
        private StructureBase MakePresentation3Resource(IMetsResource? metsResource,
            ICollection? partOfCollection,
            Work work,
            ManifestationMetadata manifestationMetadata,
            State? state, BuildResult buildResult)
        {
            switch (metsResource)
            {
                case ICollection metsCollection:
                    var collection = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(metsCollection.Identifier!)
                    };
                    AddCommonMetadata(collection, work, manifestationMetadata);
                    BuildCollection(collection, metsCollection, work, manifestationMetadata, state);
                    return collection;
                case IManifestation metsManifestation:
                    var manifest = new Manifest
                    {
                        Id = uriPatterns.Manifest(metsManifestation.Identifier!)
                    };
                    if (partOfCollection != null)
                    {
                        // For multi-volume works, periodicals etc that are all under the same _package_
                        // Hierarchical archive partOf is not set here, but by AddCommonMetadata
                        manifest.PartOf = new List<ResourceBase>
                        {
                            new Collection
                            {
                                Id = uriPatterns.CollectionForWork(partOfCollection.Identifier!),
                                Label = Lang.Map(work.Title!),
                                Behavior = new List<string>{Behavior.MultiPart}
                            }
                        };
                    }
                    AddCommonMetadata(manifest, work, manifestationMetadata);
                    BuildManifest(manifest, metsManifestation, manifestationMetadata, state, buildResult);
                    return manifest;
            }
            throw new NotSupportedException("Unhandled type of Digitised Resource");
        }

        private void BuildCollection(Collection collection,
            ICollection metsCollection,
            Work work,
            ManifestationMetadata manifestationMetadata,
            State? state)
        {
            // The work label should be preferred over the METS label,
            // but sometimes there is structural (volume) labelling that the catalogue API won't know about.
            collection.Items = new List<ICollectionItem>();
            collection.Behavior ??= new List<string>();
            collection.Behavior.Add(Behavior.MultiPart);
            collection.Rendering ??= new List<ExternalResource>()
            {
                new("Text")
                {
                    Id = uriPatterns.WorkZippedText(manifestationMetadata.Identifier.PackageIdentifier),
                    Label = Lang.Map("en", "Complete text as zip file"),
                    Format = "application/zip"
                }
            };
            if (metsCollection.Collections.HasItems())
            {
                foreach (var coll in metsCollection.Collections)
                {
                    collection.Items.Add(new Collection
                    {
                        Id = uriPatterns.CollectionForWork(coll.Identifier!),
                        Label = Lang.Map(metsCollection.Label!)
                    });
                }
            }

            if (state == null)
            {
                throw new IIIFBuildStateException("State is required to collection");
            }

            state.RightsState = new RightsState();

            if (metsCollection.Manifestations.HasItems())
            {
                int counter = 1;
                foreach (var metsManifestation in metsCollection.Manifestations)
                {
                    var type = metsManifestation.Type;
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
                        state.AVState.MultipleManifestationMembers.Add(
                            new MultipleManifestationMember(metsManifestation.Identifier!, type));
                    }
                    var order = metsManifestation.Order;
                    if (!order.HasValue || order < 1)
                    {
                        order = counter;
                    }

                    collection.Items.Add(new Manifest
                    {
                        Id = uriPatterns.Manifest(metsManifestation.Identifier!),
                        Label = new LanguageMap
                        {
                            ["en"] = new()
                            {
                                $"Volume {order}",
                                work.Title!
                            }
                        },
                        Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(metsManifestation.Identifier!)
                    });
                    counter++;
                }
            }
        }
        
        private void BuildManifest(Manifest manifest,
            IManifestation metsManifestation,
            ManifestationMetadata manifestationMetadata,
            State? state, BuildResult buildResult)
        {
            manifest.Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(metsManifestation.Identifier!);
            build.RequiredStatement(manifest, metsManifestation, manifestationMetadata, ddsOptions.UseRequiredStatement);
            build.Rights(manifest, metsManifestation);
            build.PagedBehavior(manifest, metsManifestation);
            build.ViewingDirection(manifest, metsManifestation); // do we do this?
            build.Rendering(manifest, metsManifestation);
            build.SearchServices(manifest, metsManifestation);
            build.Canvases(manifest, metsManifestation, state, buildResult);
            // do this next... both the next two use the manifestStructureHelper
            build.Structures(manifest, metsManifestation); // ranges
            build.ImprovePagingSequence(manifest);
            build.CheckForCopyAndVolumeStructure(metsManifestation, state);
            build.ManifestLevelAnnotations(manifest, metsManifestation, ddsOptions.BuildWholeManifestLineAnnotations);
            build.AddAccessHint(manifest, metsManifestation, manifestationMetadata.Identifier);
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
            ManifestationMetadata? manifestationMetadata)
        {
            if (ddsOptions.LinkedDataDomain.IsNullOrWhiteSpace())
            {
                throw new FormatException("Missing LinkedDataDomain in DdsOptions");
            }
            // Do this at serialisation time - but check
            // iiifResource.EnsurePresentation3Context();
            iiifResource.Label = Lang.Map(work.Title!);
            build.SeeAlso(iiifResource, work);
            iiifResource.AddWellcomeProvider(ddsOptions.LinkedDataDomain);
            if (manifestationMetadata != null)
            {
                iiifResource.AddOtherProvider(manifestationMetadata, ddsOptions.LinkedDataDomain);
                build.Aggregations(iiifResource, manifestationMetadata);
                build.AddTrackingLabel(iiifResource, manifestationMetadata);
            }
            build.Summary(iiifResource, work);
            build.HomePage(iiifResource, work);
            build.Metadata(iiifResource, work);
            build.ArchiveCollectionStructure(iiifResource, work, 
                () => dds.GetManifestationsForChildren(work.ReferenceNumber));
            build.AddTimestampService(iiifResource);
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
                ImageAnnotations = new()
                {
                    Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(manifestation.Identifier, 3),
                    Items = new List<IAnnotation>()
                },
                PageAnnotations = new IIIF.Presentation.V3.Annotation.AnnotationPage[annotationPages.Count],
                // OA
                OpenAnnotationImageAnnotations = new ()
                {
                    Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(manifestation.Identifier, 2),
                    Resources = new List<IAnnotation>()
                }
            };
            if (ddsOptions.BuildWholeManifestLineAnnotations)
            {
                result.AllContentAnnotations = new()
                {
                    Id = uriPatterns.ManifestAnnotationPageAllWithVersion(manifestation.Identifier, 3),
                    Items = new List<IAnnotation>()
                };
                result.AllContentAnnotations.EnsurePresentation3Context();
                result.OpenAnnotationAllContentAnnotations = new()
                {
                    Id = uriPatterns.ManifestAnnotationPageAllWithVersion(manifestation.Identifier, 2),
                    Resources = new List<IAnnotation>()
                };
                result.OpenAnnotationAllContentAnnotations.EnsurePresentation2Context();
            }
            
            result.ImageAnnotations.EnsurePresentation3Context();
            result.OpenAnnotationImageAnnotations.EnsurePresentation2Context();
            for (var i = 0; i < annotationPages.Count; i++)
            {
                var altoPage = annotationPages[i];
                var w3CPage = new IIIF.Presentation.V3.Annotation.AnnotationPage
                {
                    Id = uriPatterns.CanvasOtherAnnotationPageWithVersion(manifestation.Identifier, altoPage.AssetIdentifier, 3),
                    Items = new List<IAnnotation>()
                };
                w3CPage.EnsurePresentation3Context();
                string canvasId = uriPatterns.Canvas(manifestation.Identifier, altoPage.AssetIdentifier);

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
                    // NB this won't have any effect if the TargetConverter Serialiser is in use
                    firstAnnoCanvas.PartOf = new List<ResourceBase>
                    {
                        new Manifest {Id = uriPatterns.Manifest(manifestation.Identifier)}
                    };
                }
                result.AllContentAnnotations?.Items?.AddRange(allW3CPageAnnotations);
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

                result.OpenAnnotationAllContentAnnotations?.Resources.AddRange(allOAPageAnnotations);
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
                Body = new ClassifyingBody(DcTypesStillImage)
                {
                    Label = Lang.Map(il.Type) // https://github.com/w3c/web-annotation/issues/437
                }
            };
        }

        private IIIF.Presentation.V2.Annotation.Annotation GetOATextLineAnnotation(
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
                    Format = "text/plain",
                    Chars = tl.Text
                }
            };
        }

        private IIIF.Presentation.V2.Annotation.Annotation GetOAIllustrationAnnotation(
            AnnotationPage altoPage, Illustration il, int index, string canvasId)
        {
            return new IIIF.Presentation.V2.Annotation.Annotation
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

        public TermList BuildTermListV1(string manifestationIdentifier, string q, string[] suggestions)
        {
            var searchUri = uriPatterns.IIIFContentSearchService1(manifestationIdentifier);
            return new TermList
            {
                Context = SearchService.Search1Context,
                Id = uriPatterns.IIIFAutoCompleteService1(manifestationIdentifier) + "?q=" + q,
                Terms = suggestions.Select(suggestion => new Term
                {
                        Match = suggestion,
                        Search = searchUri + "?q=" + suggestion
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
                string? firstBefore = null;
                string? lastAfter = null;
                string match = "";
                foreach(Rect rect in sr.Rects.AnyItems())
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
                hit.Before = firstBefore!;
                hit.After = lastAfter!;
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
                Context = SearchService.Search1Context,
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
            Hit? currentHit = null;
            int currentHitIndex = -1;
            string? after = null;
            
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
                if (currentHit != null)
                {
                    currentHit.Match += (currentHit.Match.HasText() ? " " : "") + resultRect.ContentRaw;
                }
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
                Context = SearchService.Search1Context,
                Resources = new List<IAnnotation>(resources),
                Hits = hits.ToArray(),
                Within = new SearchResultsLayer { Total = resources.Count }
            };
        }

        public AnnotationList ConvertW3CAnnoPageJsonToOAAnnoList(JObject v3, string manifestationIdentifier, string? assetIdentifier)
        {
            var annotationList = new AnnotationList
            {
                Id = uriPatterns.CanvasOtherAnnotationPageWithVersion(manifestationIdentifier, assetIdentifier, 2),
                Resources = new List<IAnnotation>()
            };
            annotationList.EnsurePresentation2Context();

            var w3CAnnoItems = v3["items"];
            if (w3CAnnoItems == null || w3CAnnoItems.Type != JTokenType.Array)
            {
                // This anno page has no items
                return annotationList;
            }
            
            foreach (var jItem in w3CAnnoItems)
            {
                var annotation = new IIIF.Presentation.V2.Annotation.Annotation
                {
                    Id = jItem.Value<string>("id"),
                    On = jItem.Value<string>("target")! // using the TargetConverter Serializer
                    // TODO: put this back when better supported in viewers
                    //On = jItem["target"]?.Value<string>("id") ?? string.Empty
                };
                // This will, atm, be either a textual body (line anno) or an image-classifying anno.
                var body = jItem["body"];
                if (body != null)
                {
                    if ("TextualBody" == body.Value<string>("type"))
                    {
                        annotation.Motivation = "sc:painting";
                        annotation.Resource = new ContentAsTextAnnotationResource
                        {
                            Chars = body.Value<string>("value")!,
                            Format = "text/plain"
                        };
                    }
                    else if (DcTypesStillImage == body.Value<string>("id"))
                    {
                        annotation.Motivation = "oa:classifying";
                        annotation.Resource = new IllustrationAnnotationResource
                        {
                            Id = "dctypes:Image",
                            Label = new MetaDataValue(body["label"]!["en"]![0]!.Value<string>()!)
                        };
                    }
                }
                annotationList.Resources.Add(annotation);
            }
            return annotationList;
        }

        public Collection? BuildArchiveNode(Work work)
        {
            if (work.ReferenceNumber.HasText())
            {
                var collection = new Collection
                {
                    Id = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber)
                };
                AddCommonMetadata(collection, work, null);
                return collection;
            }

            return null;
        }
    }
}