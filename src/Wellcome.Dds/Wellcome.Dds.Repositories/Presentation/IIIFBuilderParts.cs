using System;
using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using IIIF.Presentation.Content;
using IIIF.Presentation.Strings;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilderParts
    {
        private readonly UriPatterns uriPatterns;

        public IIIFBuilderParts(UriPatterns uriPatterns)
        {
            this.uriPatterns = uriPatterns;
        }


        public void HomePage(ResourceBase iiifResource, Work work)
        {
            iiifResource.Homepage = new List<ExternalResource>
            {
                new ExternalResource("Text")
                {
                    Id = uriPatterns.PersistentPlayerUri(work.Id),
                    Label = Lang.Map(work.Title),
                    Format = "text/html",
                    Language = new List<string>{"en"}
                }
            };
        }

        public void Aggregations(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var groups = manifestationMetadata.Metadata.GroupBy(m => m.Label);
            foreach (var @group in groups)
            {
                foreach (var md in @group)
                {
                    iiifResource.PartOf ??= new List<ResourceBase>();
                    iiifResource.PartOf.Add(
                        new Collection
                        {
                            Id = uriPatterns.CollectionForAggregation(md.Label, md.Identifier),
                            Label = new LanguageMap("en", $"{md.Label}: {md.StringValue}")
                        });
                }
            }
        }


        public void SeeAlso(ResourceBase iiifResource, Work work)
        {
            iiifResource.SeeAlso = new List<ExternalResource>
            {
                new ExternalResource("Dataset")
                {
                    Id = uriPatterns.CatalogueApi(work.Id, new string[]{}),
                    Label = Lang.Map("Wellcome Collection Catalogue API"),
                    Format = "application/json",
                    Profile = "https://api.wellcomecollection.org/catalogue/v2/context.json"
                }
            };
        }


        public void Summary(StructureBase iiifResource, Work work)
        {
            if (work.Description.HasText())
            {
                // Would this ever not be in English?
                iiifResource.Summary = Lang.Map(work.Description);
            }
        }

        public void RequiredStatement(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var usage = LicenceHelpers.GetUsageWithHtmlLinks(digitisedManifestation.MetsManifestation.ModsData.Usage);
            var permittedOps = digitisedManifestation.MetsManifestation.PermittedOperations;
            var accessCondition = digitisedManifestation.MetsManifestation.ModsData.AccessCondition;
            string attribution = "Wellcome Collection";
            
            
            // BROKEN - convert this!
            
            if (licenseInfo == null)
            {
                return null;
            }
            if (licenseInfo.Usage.HasText())
            {
                return licenseInfo.Usage; // this override the licence lookup
            }
            if (licenseInfo.LicenseCode.IsNullOrWhiteSpace())
            {
                return null;
            }
            // reuse this from Player for now:
            var dict = PlayerConfigProvider.BaseConfig.Modules.ConditionsDialogue.Content;
            return dict.ContainsKey(licenseInfo.LicenseCode) ? dict[licenseInfo.LicenseCode] : null;
        }

        public void Rights(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var dzl = digitisedManifestation.MetsManifestation.ModsData.DzLicenseCode;
            var code = LicenceCodes.MapLicenseCode(dzl);
            var uri = LicenseMap.GetLicenseUri(code);

        }

        public void PagedBehavior(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void ViewingDirection(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void Rendering(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void SearchServices(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void ServicesForAuth(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void Structures(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void ManifestLevelAnnotations(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void Canvases(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void Metadata(ResourceBase iiifResource, Work work)
        {
            // throw new NotImplementedException();
        }

        public void ArchiveCollectionStructure(ResourceBase iiifResource, Work work)
        {
            // throw new NotImplementedException();
        }
    }
}