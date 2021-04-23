using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V1;
using Utils;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
using Wellcome.Dds.Repositories.Presentation.V2.IXIF;
using ExternalResource = IIIF.Presentation.V3.Content.ExternalResource;
using Presi3 = IIIF.Presentation.V3;

namespace Wellcome.Dds.Repositories.Presentation.V2
{
    /// <summary>
    /// A collection of helper functions to help keep <see cref="PresentationConverter"/> more lightweight.
    /// </summary>
    public class ConverterHelpers
    {
        // Remove this as soon as we can!
        const bool DuplicateAuthServices = true;
        
        public static T GetIIIFPresentationBase<T>(Presi3.StructureBase resourceBase, Func<string, bool>? labelFilter = null)
            where T : IIIFPresentationBase, new()
        {
            // NOTE - using assignment statements rather than object initialiser to get line numbers for any errors
            var presentationBase = new T();
            presentationBase.Id = resourceBase.Id;
            presentationBase.Description = MetaDataValue.Create(resourceBase.Summary, true);
            presentationBase.Label = MetaDataValue.Create(resourceBase.Label, true, labelFilter);
            presentationBase.License = resourceBase.Rights;
            presentationBase.Metadata = ConvertMetadata(resourceBase.Metadata);
            presentationBase.NavDate = resourceBase.NavDate;
            presentationBase.Related = resourceBase.Homepage?.Select(ConvertResource).ToList();
            presentationBase.SeeAlso = resourceBase.SeeAlso?.Select(ConvertResource).ToList();
            presentationBase.Within = resourceBase.PartOf?.FirstOrDefault()?.Id;

            if (resourceBase.Service.HasItems())
            {
                presentationBase.Service = resourceBase.Service!.Select(s => ObjectCopier.DeepCopy(s, service =>
                {
                    if (service is ResourceBase serviceResourceBase)
                        serviceResourceBase.Type = null;
                    if (service is SearchService searchService)
                    {
                        // we need to make it match Wellcome published IIIF 2,
                        // which uses a precursor v0 Search API
                        var v0Context = SearchService.Search1Context.Replace("/search/1/", "/search/0/");
                        searchService.EnsureContext(v0Context);
                        if (searchService.Service != null)
                            searchService.Service.Type = null;
                        searchService.Id = searchService.Id.Replace("/search/v1/", "/search/v0/");
                        searchService.Profile = searchService.Profile.Replace("/search/1/", "/search/0/");
                        searchService.Service.Id = searchService.Service.Id.Replace("/autocomplete/1/", "/autocomplete/0/");
                        searchService.Service.Profile = searchService.Service.Profile.Replace("/search/1/", "/search/0/");
                    }
                })!).ToList();
            }

            presentationBase.Profile = resourceBase.Profile;
            presentationBase.Thumbnail = ConvertThumbnails(resourceBase.Thumbnail);

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                // Wellcome will always be the first provider
                presentationBase.Logo = resourceBase.Provider!.First().Logo?.FirstOrDefault()?.Id;
            }
            
            if (!resourceBase.Annotations.IsNullOrEmpty())
            {
                presentationBase.OtherContent = resourceBase.Annotations?
                    .Select(a => new AnnotationListReference
                    {
                        Id = ToPresentationV2Id(a.Id),
                        Label = MetaDataValue.Create(a.Label, true),
                    })
                    .Cast<IAnnotationListReference>()
                    .ToList();
            }

            var attributionAndUsage = GetAttributionAndUsageMetadata(resourceBase);
            
            if (attributionAndUsage != null)
            {
                AddAttributionAndUsageMetadata(resourceBase, presentationBase, attributionAndUsage);
            }
            
            return presentationBase;
        }

        private static LabelValuePair? GetAttributionAndUsageMetadata(Presi3.StructureBase resourceBase)
        {
            if (resourceBase.RequiredStatement != null)
            {
                return resourceBase.RequiredStatement;
            }

            if (resourceBase.Metadata.IsNullOrEmpty())
            {
                return null;
            }

            return resourceBase.Metadata.FirstOrDefault(IsAttributionAndUsage);
        }

        private static bool IsAttributionAndUsage(LabelValuePair pair)
        {
            return pair.Label.Values.HasItems() 
                   && pair.Label.Values.First().First() == Constants.AttributionAndUsage;
        }

        public static string ToPresentationV2Id(string? id)
            => id?.Replace("/presentation/", "/presentation/v2/")
                  .Replace("/annotations/v3/", "/annotations/v2/")
               ?? string.Empty;

        public static bool PopulateFromBody(AnnotationListForMedia annoListForMedia,
            IPaintable paintable,
            WellcomeAuthServiceManager authServiceManager,
            bool populated = false)
        {
            annoListForMedia.Rendering ??= new List<IIIF.Presentation.V2.ExternalResource>();
            
            if (paintable is PaintingChoice choice)
            {
                foreach (var i in choice.Items?? Enumerable.Empty<IPaintable>())
                {
                    populated = PopulateFromBody(annoListForMedia, i, authServiceManager, populated);
                }
            }
            else
            {
                var externalResource = (ExternalResource)paintable;
                var copiedServices = externalResource.Service?
                    .Select(i => ObjectCopier.DeepCopy(i))
                    .ToList();
                var avResource = new ExternalResourceForMedia
                {
                    Id = externalResource.Id,
                    Format = externalResource.Format,
                    Service = copiedServices,
                };
                PopulateAuthServices(authServiceManager, avResource.Service, true, true);
                annoListForMedia.Rendering.Add(avResource);
                annoListForMedia.Service = avResource.Service;

                if (populated) return populated;
                
                annoListForMedia.Service = avResource.Service;
                annoListForMedia.Id = $"{avResource.Id}#identity";
                annoListForMedia.Type = paintable is Video ? "dctypes:MovingImage" : "dctypes:Sound";
                annoListForMedia.Format = avResource.Format;

                if (paintable is ISpatial spatial)
                {
                    annoListForMedia.Width = spatial.Width;
                    annoListForMedia.Height = spatial.Height;
                }

                if (paintable is ITemporal temporal)
                {
                    annoListForMedia.Metadata = new List<IIIF.Presentation.V2.Metadata>
                    {
                        new()
                        {
                            Label = new MetaDataValue("length"),
                            Value = new MetaDataValue($"{temporal.Duration} s") // TODO - convert to Xmn Ys
                        }
                    };
                }

                populated = true;
            }

            return populated;
        }
        
        public static ImageAnnotation GetImageAnnotation(Image image, PaintingAnnotation paintingAnnotation, Canvas canvas,
            WellcomeAuthServiceManager authServiceManager)
        {
            // Copy all services over, these will be ImageService2 and potentially ServiceReference for auth
            ImageService2? imageService = null;
            
            // Get all non-serviceReference services (service reference will be auth services)
            // we only want auth in ImageService
            var services = image.Service?
                .Where(s => s.GetType() != typeof(V2ServiceReference)).ToList()
                .Select(i => ObjectCopier.DeepCopy(i, s =>
                {
                    if (s is ImageService2 imageService2)
                    {
                        imageService2.EnsureContext(ImageService2.Image2Context);
                        imageService2.Type = null;
                        imageService2.Protocol = ImageService2.Image2Protocol;
                        imageService = imageService2;
                    }
                }))
                .ToList();

            var addedAuthServices = PopulateAuthServices(authServiceManager, imageService.Service, false, DuplicateAuthServices);
            // in wl.org manifests, if the image resource's image service has auth services,
            // then so does the image itself.
            if (addedAuthServices != null)
            {
                services?.AddRange(addedAuthServices);
            }

            var imageAnnotation = new ImageAnnotation();
            imageAnnotation.Id = paintingAnnotation.Id;
            imageAnnotation.On = canvas.Id ?? string.Empty;
            imageAnnotation.Resource = new ImageResource
            {
                Id = image.Id,
                Height = image.Height,
                Width = image.Width,
                Format = image.Format,
                Service = services
            };
            return imageAnnotation;
        }

        public static bool IsBornDigital(IIIF.Presentation.V3.Manifest p3Manifest) 
            => p3Manifest.Items.HasItems() && p3Manifest.Items!.All(item => item.Items.IsNullOrEmpty());
        
        private static List<IService>? PopulateAuthServices(WellcomeAuthServiceManager authServiceManager,
            List<IService>? candidateServices, bool forceFullService, bool duplicateAuthServices)
        {
            // If we don't have auth services then bail out..
            if (!authServiceManager.HasItems) return null;

            // Get all service references from candidate-services (these will be auth services)
            // for P3 we will _only_ have svc refs, the main Auth services will be in the manifest.Service element 
            var authServiceReferences = candidateServices?.OfType<V2ServiceReference>().ToList();
            if (!authServiceReferences.HasItems()) return null;

            // Remove these from imageService.Services
            candidateServices?.RemoveAll(s => s is V2ServiceReference);

            // Re-add appropriate services. This will be full AuthService if first time it appears,
            // or serviceReference if it is not the first time
            List<IService> addedAuthServices = new();
            foreach (var authRef in authServiceReferences!)
            {
                var service = authServiceManager.Get(authRef.Id!, forceFullService);
                if (service is WellcomeAccessControlHintService was)
                {
                    // if we have a wellcomeAuthService add the "AuthService" only
                    candidateServices?.AddRange(was.AuthService);
                    addedAuthServices.AddRange(was.AuthService);
                    if (duplicateAuthServices)
                    {
                        candidateServices?.AddRange(was.AuthService);
                        addedAuthServices.AddRange(was.AuthService);
                    }
                }
                else
                {
                    // else just re-add the reference
                    candidateServices?.Add(service);
                    addedAuthServices.Add(service);
                    if (duplicateAuthServices)
                    {
                        candidateServices?.Add(service);
                        addedAuthServices.Add(service);
                    }
                }
            }

            return addedAuthServices;
        }
        
        private static void AddAttributionAndUsageMetadata<T>(
            IIIF.Presentation.V3.StructureBase resourceBase,
            T presentationBase,
            LabelValuePair attributionAndUsage)
            where T : IIIFPresentationBase, new()
        {
            presentationBase.Metadata ??= new List<IIIF.Presentation.V2.Metadata>();

            var attributionAndUsageValue = attributionAndUsage!.Value.SelectMany(rs => rs.Value).ToList();
            if (!attributionAndUsageValue.HasItems()) return;

            // Conditions of use is last section of requiredStatement
            var conditionsOfUse = attributionAndUsageValue.Count > 1 ? attributionAndUsageValue.Last() : string.Empty;

            // more than 1 provider means wellcome + _other_, so use other as attribution
            bool isNotWellcome = resourceBase.Provider!.Count > 1;
            var agent = resourceBase.Provider!.Last();

            // Attribution is the first element of the requiredStatement
            var attribution = isNotWellcome ? agent.Label!.ToString() : Constants.WellcomeCollection;

            // Check to see if we can get license to add to Attribution
            var license = LicenseMap.GetLicenseAbbreviation(presentationBase.License ?? string.Empty);
            if (string.IsNullOrEmpty(license))
            {
                if (conditionsOfUse.Contains(Constants.InCopyrightStatement))
                {
                    // "in copyright" works don't have a .License so need to look at conditions of use to determine value
                    license = Constants.InCopyrightCondition;
                }
                else if (conditionsOfUse.Contains(Constants.CopyrightNotClearedStatement))
                {
                    license = Constants.CopyrightNotClearedCondition;
                }
            }

            presentationBase.Metadata.Add(new IIIF.Presentation.V2.Metadata
            {
                Label = new MetaDataValue("Attribution"),
                Value = new MetaDataValue(string.IsNullOrEmpty(license)
                    ? attribution
                    : $"{attribution}<br/>License: {license}")
            });

            presentationBase.Metadata.Add(new IIIF.Presentation.V2.Metadata
            {
                Label = new MetaDataValue("Full conditions of use"),
                Value = new MetaDataValue(conditionsOfUse)
            });

            if (isNotWellcome)
            {
                // add repository if ! wellcome
                var logo = agent.Logo?.FirstOrDefault()?.Id;
                var licenseText = attributionAndUsageValue.First();
                var repository =
                    $"<img src='{logo}' alt='{attribution}' /><br/><br/>{licenseText}";
                presentationBase.Metadata.Add(new IIIF.Presentation.V2.Metadata
                {
                    Label = new MetaDataValue("Repository"),
                    Value = new MetaDataValue(repository)
                });
            }
        }

        private static List<Thumbnail>? ConvertThumbnails(List<ExternalResource>? thumbnails)
        {
            if (thumbnails.IsNullOrEmpty()) return null;

            return thumbnails!.Select(t => new Thumbnail
            {
                Service = t.Service?.OfType<ImageService2>()
                    .Select(i => ObjectCopier.DeepCopy(i, service2 =>
                    {
                        service2.EnsureContext(ImageService2.Image2Context);
                        service2.Protocol = ImageService2.Image2Protocol;
                        service2.Type = null;
                    }))
                    .Cast<IService>()
                    .ToList(),
                Id = t.Id
            }).ToList();
        }

        private static Resource ConvertResource(ExternalResource externalResource)
        {
            var resource = new Resource();
            resource.Id = externalResource.Id;
            resource.Label = MetaDataValue.Create(externalResource.Label, true);
            resource.Format = externalResource.Format;
            resource.Profile = externalResource.Profile;
            resource.Service = ObjectCopier.DeepCopy(externalResource.Service);
            return resource;
        }

        private static List<IIIF.Presentation.V2.Metadata>? ConvertMetadata(List<LabelValuePair>? presi3Metadata)
        {
            if (presi3Metadata.IsNullOrEmpty()) return null;

            return presi3Metadata!
                .Where(pair => !IsAttributionAndUsage(pair))
                .Select(p => new IIIF.Presentation.V2.Metadata
                {
                    Label = new MetaDataValue(p.Label.Join("; ")),
                    Value = new MetaDataValue(p.Value.Join("; "))
                })
                .ToList();
        }
    }
}