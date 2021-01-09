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

        public List<IStoredFile> SynchronisableFiles
        {
            get
            {
                LazyInit();
                return synchronisableFiles;
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
                // ModsData will be null if Partial == true
                if (ModsData != null && ModsData.PlayerOptions > 0)
                {
                    return LicensesAndOptions.Instance.GetPermittedOperations(
                        ModsData.PlayerOptions, FirstSignificantInternetType);
                }
                if (ModsData != null && ModsData.DzLicenseCode.HasText())
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
        private List<IStoredFile> synchronisableFiles;
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
                    // When we want to include POSTER images in the DLCS sync operation, 
                    // we can add || f.Use == "POSTER" here. Wait till new DLCS before doing that.
                    synchronisableFiles = sequence.SelectMany(pf => pf.Files)
                        .Where(sf => 
                            sf.Use == "OBJECTS" ||  // Old workflows
                            sf.Use == "ACCESS" ||   // New workflows
                            sf.Use == "TRANSCRIPT")
                        .ToList();
                    var ignoredFiles = sequence.SelectMany(pf => pf.Files)
                        .Where(sf => 
                            sf.Use == "POSTER" || 
                            sf.Use == "ALTO" || 
                            sf.Use == "PRESERVATION")
                        .ToList();
                    
                    // if we're not going to see any "sequence" MXF files, then:
                    ignoredStorageIdentifiers = ignoredFiles.Select(sf => sf.StorageIdentifier).ToList();
                    // but for now:
                    ignoredStorageIdentifiers = ignoredStorageIdentifiers
                        .Union(ignoreAssetFilter.GetStorageIdentifiersToIgnore(Type, sequence)).ToList();
                    // and the notion of SignificantSequence can go again (?)
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
