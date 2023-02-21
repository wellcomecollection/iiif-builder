using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PhysicalFile : IPhysicalFile
    {
        // Only set AssetFamily to Image for born digital mimetypes image/{type} where {type} is one of:
        private static readonly string[] BornDigitalImageTypes = { "jpeg", "tiff" };

        public PhysicalFile()
        {
            
        }
        private PhysicalFile(IWorkStore workStore, string id)
        {
            WorkStore = workStore;
            Id = id;
        }
        
        public static IPhysicalFile FromBornDigitalMets(
            XElement rootElement,
            XElement fileElement,
            IWorkStore workStore)
        {
            var fileId = (string?)fileElement.Attribute("ID") ?? 
                         throw new InvalidOperationException("Physical File Element must have ID attribute");
            var relativePath = GetLinkRef(fileElement) ?? 
                         throw new InvalidOperationException("No relative path obtainable from physical file element");     
            var physicalFile = new PhysicalFile(workStore, fileId)
            {
                Files        = new List<IStoredFile>(),
                RelativePath = relativePath
            };

            string? admId = fileElement.Attribute("ADMID")?.Value;
            if (admId.IsNullOrWhiteSpace())
            {
                throw new NotSupportedException($"File element {physicalFile.Id} has no ADMID attribute.");
            }

            physicalFile.AssetMetadata = workStore.MakeAssetMetadata(rootElement, admId);
            // Will we eventually have directory-level access conditions that apply to all their files?
            var accessConditionFromRights = physicalFile.AssetMetadata.GetRightsStatement().AccessCondition;
            physicalFile.AccessCondition = accessConditionFromRights ?? 
                                           throw new InvalidOperationException("No Access Condition available from Rights statement");
            physicalFile.OriginalName = physicalFile.AssetMetadata.GetOriginalName();
            physicalFile.StorageIdentifier = GetSafeStorageIdentifierForBornDigital(workStore.Identifier, physicalFile.RelativePath);
            physicalFile.MimeType = physicalFile.AssetMetadata.GetMimeType(); 
            physicalFile.CreatedDate = physicalFile.AssetMetadata.GetCreatedDate();
            physicalFile.Family = physicalFile.MimeType.GetAssetFamily(BornDigitalImageTypes);
                
            // for BD there is only one StoredFile per PhysicalFile
            var file = new StoredFile
            {
                Id = physicalFile.Id,
                WorkStore = workStore,
                PhysicalFile = physicalFile,
                AssetMetadata = physicalFile.AssetMetadata,
                RelativePath = physicalFile.RelativePath,
                StorageIdentifier = physicalFile.StorageIdentifier,
                MimeType = physicalFile.MimeType,
                Family = physicalFile.Family,
                Use = fileElement.Parent?.Attribute("USE")?.Value
            };
            physicalFile.Files.Add(file);
            
            return physicalFile;
        }

        public static IPhysicalFile FromDigitisedMets(
            XElement physFileElement,
            Dictionary<string, XElement> fileMap,
            IWorkStore workStore)
        {
            var metsRoot = physFileElement.AncestorsAndSelf().Last();
            var fileId = (string?)physFileElement.Attribute("ID") ??
                         throw new InvalidOperationException("Physical file element must have ID attribute");
            var physicalFile = new PhysicalFile(workStore, fileId)
            {
                Type       = (string?) physFileElement.Attribute("TYPE") ?? 
                             throw new InvalidOperationException("Physical file element must have TYPE attribute"),
                Order      = (int?)    physFileElement.Attribute("ORDER"),
                OrderLabel = (string?) physFileElement.Attribute("ORDERLABEL"),
                Files      = new List<IStoredFile>()
            };

            // When the link to technical metadata is declared on the physical file element itself
            // This is always the case until the new AV workflow.
            string? admId = (string?) physFileElement.Attribute("ADMID");
            if (admId.HasText())
            {
                physicalFile.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
            }
            else
            {
                // TODO: this is temporary code to get round Intranda naming the attribute incorrectly
                // it can be removed later
                string? amdId = (string?) physFileElement.Attribute("AMDID");
                if (amdId.HasText())
                {
                    physicalFile.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, amdId);
                }
            }
            // at this point, AssetMetadata will still be null for the new AV workflow
            
            
            var filePointers = physFileElement.Elements(XNames.MetsFptr);
            foreach (var pointer in filePointers)
            {
                var file = new StoredFile
                {
                    Id = (string?) pointer.Attribute("FILEID"),
                    WorkStore = workStore,
                    PhysicalFile = physicalFile
                };
                physicalFile.Files.Add(file);

                if (file.Id.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("File pointer must point at physical file");
                }
                var fileElement = fileMap[file.Id];
                file.Use = fileElement.Parent!.Attribute("USE")!.Value;
                admId = (string?) fileElement.Attribute("ADMID");
                if (admId.HasText())
                {
                    file.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
                }
                file.RelativePath = GetLinkRef(fileElement);
                if (file.RelativePath.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("Could not obtain relative path from file element");
                }
                file.StorageIdentifier = GetSafeStorageIdentifierForDigitised(file.RelativePath, workStore);
                file.MimeType = (string?) fileElement.Attribute("MIMETYPE");
                file.Family = file.MimeType.GetAssetFamily();

                switch (file.Use)
                {
                    case "ALTO":
                        physicalFile.RelativeAltoPath = file.RelativePath; 
                        break;
                    
                    case "POSTER":
                        physicalFile.RelativePosterPath = file.RelativePath;
                        break;
                    
                    case "PRESERVATION":
                    case "MASTER":
                        physicalFile.RelativeMasterPath = file.RelativePath;
                        break;
                    
                    case "TRANSCRIPT":
                        physicalFile.RelativeTranscriptPath = file.RelativePath;
                        break;
                        
                    default:
                        // Assume that anything else (it should be ACCESS) is the access version,
                        // and the source of the physical file properties.
                        physicalFile.AssetMetadata ??= file.AssetMetadata;
                        physicalFile.StorageIdentifier = file.StorageIdentifier;
                        // So... here we set MimeType from the file element.
                        // But, we are using the Premis IAssetMetadata mimetype to guide how we find height and width...
                        // This will be fine as long as they give the same result.
                        // TODO: MimeType determination, revisit.
                        physicalFile.MimeType = file.MimeType;
                        physicalFile.RelativePath = file.RelativePath;
                        physicalFile.Family = file.Family;
                        break;
                }
            }

            return physicalFile;
        }

        private static string? GetLinkRef(XElement fileElement)
        {
            var xElement = fileElement.Element(XNames.MetsFLocat);
            string? linkHref = null;
            var xAttribute = xElement?.Attribute(XNames.XLinkHref);
            if (xAttribute != null)
                linkHref = xAttribute.Value;
            if (!linkHref.HasText())
                linkHref = null;
            return linkHref;
        }
        
        
        private static string GetSafeStorageIdentifierForBornDigital(string identifier, string relativePath)
        {
            // This implementation is TBC!
            // We have a problem with path lengths here.
            // We WILL encounter paths longer than this and we need a strategy for dealing with them and still
            // produce a guaranteed unique ID.
            // we choose to build this from relativePath to use whatever normalisation Archivematica has already done
            // We could choose to do this from the originalName.

            var id = relativePath.RemoveStart("objects/");
            id = id!.Replace("/", "---");
            id = id.Replace(" ", "_");
            id = identifier.Replace("/", "_") + "---" + id;
            if (id.Length > 220) // this can be longer, we'll see what happens when we run into it.
            {
                throw new NotSupportedException($"Identifier longer than 220: {id}");
            }

            return id;
        }

        private static string? GetSafeStorageIdentifierForDigitised(string fullPath, IWorkStore workStore)
        {
            const string objectsPart = "objects/";
            const int pos = 8;
            string? storageIdentifier;
            if (fullPath.StartsWith(objectsPart))
            {
                storageIdentifier = fullPath.Substring(pos).Replace('/', '_');
            }
            else
            {
                storageIdentifier = PathStringUtils.GetSimpleNameFromPath(fullPath);
            }
            if (storageIdentifier != null && !storageIdentifier.StartsWith(workStore.Identifier, StringComparison.InvariantCultureIgnoreCase))
            {
                storageIdentifier = $"{workStore.Identifier}_{storageIdentifier}";
            }
            return storageIdentifier;
        }


        public IWorkStore WorkStore { get; set; }
        public string Id { get; set; }
        public string? Type { get; set; }
        public string? OriginalName { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? Order { get; set; }
        public int Index { get; set; }
        public string? OrderLabel { get; set; }

        // we need to replace SdbId with StorageIdentifier
        //public Guid SdbId { get; set; }
        public string? StorageIdentifier { get; set; }

        public string? MimeType { get; set; }
        public IAssetMetadata? AssetMetadata { get; set; }

        // These two fields are obtained from MODS
        public string? AccessCondition { get; set; }

        public List<IStoredFile>? Files { get; set; }
        
        public string? RelativePath { get; set; }
        public string? RelativeAltoPath { get; set; }
        public string? RelativePosterPath { get; set; }
        public string? RelativeTranscriptPath { get; set; }
        public string? RelativeMasterPath { get; set; }

        public AssetFamily Family { get; set; }

        public IArchiveStorageStoredFileInfo GetStoredFileInfo()
        {
            return WorkStore.GetFileInfoForPath(RelativePath!);
        }

        public IArchiveStorageStoredFileInfo? GetStoredAltoFileInfo()
        {
            if (!RelativeAltoPath.HasText())
            {
                return null;
            }
            return WorkStore.GetFileInfoForPath(RelativeAltoPath);
        }
        

        public override string ToString()
        {
            return string.Format("{4} / {0} AC={7} TYPE={1} Order:{2} ({3}) MIME={5} ALTO={6}",
                Id, Type, Order, OrderLabel, StorageIdentifier, MimeType, RelativeAltoPath, AccessCondition);
        }

        /// <summary>
        /// Gather all the mets:file elements and store them by ID attribute to avoid repeated traversal
        /// </summary>
        /// <param name="rootElement"></param>
        /// <returns></returns>
        public static Dictionary<string, XElement> MakeFileMap(XElement rootElement)
        {
            var fileElements = rootElement
                .Element(XNames.MetsFileSec)
                ?.Descendants(XNames.MetsFile)
                .ToList();
            if (fileElements.IsNullOrEmpty())
            {
                throw new InvalidOperationException("METS file section contains no files");
            }
            return fileElements
                .ToDictionary(file => (string?) file.Attribute("ID") 
                                      ?? throw new InvalidOperationException("File Element has no ID attribute"));
        }

        private IProcessingBehaviour? processingBehaviour;
        public IProcessingBehaviour ProcessingBehaviour => processingBehaviour ??= new ProcessingBehaviour(
            this, new ProcessingBehaviourOptions());
    }
    
}
