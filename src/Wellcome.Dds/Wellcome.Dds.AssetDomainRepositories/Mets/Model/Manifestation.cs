using System.Collections.Generic;
using System.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    /// <summary>
    /// A bridge to IIIF. Updates the Sequence Physical File Ids with access conditions obtained from the sections
    /// </summary>
    public class Manifestation : BaseMetsResource, IManifestation
    {
        public List<IPhysicalFile> Sequence
        {
            get
            {
                LazyInit();
                return sequence;
            }
            set { sequence = value; }
        }

        public List<IPhysicalFile> SignificantSequence
        {
            get
            {
                LazyInit();
                return significantSequence;
            }
        }

        public IStructRange RootStructRange
        {
            get
            {
                LazyInit();
                return rootStructRange;
            }
            set { rootStructRange = value; }
        }

        
        public string FirstSignificantInternetType
        {
            get
            {
                LazyInit();
                return firstSignificantInternetType;
            }
        }

        public List<string> IgnoredStorageIdentifiers
        {
            get
            {
                LazyInit();
                return ignoredStorageIdentifiers;
            }
        }

        
            
        public string[] PermittedOperations
        {
            get
            {
                if (ParentModsData != null && Type == "PeriodicalIssue")
                {
                    if (ParentModsData.PlayerOptions > 0)
                    {
                        return LicensesAndOptions.Instance.GetPermittedOperations(
                            ParentModsData.PlayerOptions, FirstSignificantInternetType);
                    }
                }
                if (ModsData.PlayerOptions > 0)
                {
                    return LicensesAndOptions.Instance.GetPermittedOperations(
                        ModsData.PlayerOptions, FirstSignificantInternetType);
                }
                if (ModsData.DzLicenseCode.HasText())
                {
                    return LicensesAndOptions.Instance.GetPermittedOperations(
                        ModsData.DzLicenseCode, Type, FirstSignificantInternetType);
                }
                return new string[] {};
            }
        }


        public IStoredFile PosterImage
        {
            get
            {
                LazyInit();
                return posterImage;
            }
            set { posterImage = value; }
        }

        private Dictionary<string, IPhysicalFile> ByFileId { get; set; }
        private ILogicalStructDiv logicalStructDiv;
        private ILogicalStructDiv parentLogicalStructDiv;
        private IStoredFile posterImage;
        private bool initialised;
        private List<IPhysicalFile> sequence;
        private List<IPhysicalFile> significantSequence;
        private IStructRange rootStructRange;
        private List<string> ignoredStorageIdentifiers;
        private string firstSignificantInternetType;

        public Manifestation(ILogicalStructDiv structDiv, ILogicalStructDiv parentStructDiv = null)
        {
            logicalStructDiv = structDiv;
            Id = logicalStructDiv.ExternalId;
            ModsData = logicalStructDiv.GetMods();
            parentLogicalStructDiv = parentStructDiv;
            if (parentLogicalStructDiv != null)
            {
                ParentModsData = parentLogicalStructDiv.GetMods();
            }
            Label = GetLabel(logicalStructDiv, ModsData);
            Type = logicalStructDiv.Type;
            Order = logicalStructDiv.Order;
            SourceFile = structDiv.WorkStore.GetFileInfoForPath(structDiv.ContainingFileRelativePath); 
            if (logicalStructDiv.HasChildLink())
            {
                Partial = true;
            }
        }

        private void LazyInit()
        {
            if (!Partial && !initialised)
            {
                sequence = logicalStructDiv.GetPhysicalFiles();
                posterImage = logicalStructDiv.GetPosterImage();
                ByFileId = sequence.ToDictionary(pf => pf.Id);
                rootStructRange = BuildStructRange(logicalStructDiv);
                var ignoreAssetFilter = new IgnoreAssetFilter();
                if (sequence.HasItems())
                {
                    ignoredStorageIdentifiers = ignoreAssetFilter.GetStorageIdentifiersToIgnore(Type, sequence);
                    significantSequence = sequence.Where(pf => !ignoredStorageIdentifiers.Contains(pf.StorageIdentifier)).ToList();
                    var firstSignificantFile = significantSequence.FirstOrDefault();
                    if (firstSignificantFile != null)
                    {
                        firstSignificantInternetType = firstSignificantFile.MimeType.ToLowerInvariant().Trim();
                    }
                }
                else
                {
                    ignoredStorageIdentifiers = new List<string>(0);
                    significantSequence = new List<IPhysicalFile>(0);
                }
            }
            initialised = true;
        }

        private IStructRange BuildStructRange(ILogicalStructDiv div)
        {
            var mods = div.GetMods();
            var sr = new StructRange
            {
                Id = div.Id,
                Mods = mods,
                Label = GetLabel(div, mods),
                Type = div.Type,
                PhysicalFileIds = div.GetPhysicalFiles().Select(pf => pf.Id).ToList()
            };

            if (mods != null && mods.AccessCondition.HasText())
            {
                foreach (var fileId in sr.PhysicalFileIds)
                {
                    var file = ByFileId[fileId];
                    if (!file.AccessCondition.HasText())
                    {
                        file.AccessCondition = mods.AccessCondition;
                    }
                    else
                    {
                        // only replace if this one is more restrictive
                        var forCompare = new[] {file.AccessCondition, mods.AccessCondition};
                        file.AccessCondition = AccessCondition.GetMostSecureAccessCondition(forCompare);
                    }
                }
            }
            if (div.Children.HasItems())
            {
                sr.Children = div.Children.Select(BuildStructRange).ToList();
            }
            return sr;
        }
    }
}
