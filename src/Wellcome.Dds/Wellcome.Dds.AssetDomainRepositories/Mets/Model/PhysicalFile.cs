﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PhysicalFile : IPhysicalFile
    {
        public static IPhysicalFile FromBornDigitalMets(
            XElement rootElement,
            XElement fileElement,
            IWorkStore workStore)
        {
            var physicalFile = new PhysicalFile
            {
                WorkStore    = workStore,
                Id           = (string) fileElement.Attribute("ID"),
                Files        = new List<IStoredFile>(),
                RelativePath = GetLinkRef(fileElement)
            };

            string admId = fileElement.Attribute("ADMID")?.Value;
            if (admId.IsNullOrWhiteSpace())
            {
                throw new NotSupportedException($"File element {physicalFile.Id} has no ADMID attribute.");
            }

            // process Premis
            // We don't yet know if this is structured the same!
            physicalFile.AssetMetadata = workStore.MakeAssetMetadata(rootElement, admId);
            
            // mods - this isn't MODS but will also come from the Premis block.
            // this will give us our access conditions.
            
            
            // set other properties of physicalFile that are required and being set in the Goobi version

            // DANGER
            physicalFile.StorageIdentifier = "TBC"; // BD Naming convention compare with GetSafeStorageIdentifier below
            physicalFile.MimeType = "xxx"; // This will have to be obtained from PREMIS not an attribute... see FITS data.
            physicalFile.Family = AssetFamily.File; // OK?
            
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
                Family = physicalFile.Family
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
            var physicalFile = new PhysicalFile
            {
                WorkStore  = workStore,
                Id         = (string) physFileElement.Attribute("ID"),
                Type       = (string) physFileElement.Attribute("TYPE"),
                Order      = (int?)   physFileElement.Attribute("ORDER"),
                OrderLabel = (string) physFileElement.Attribute("ORDERLABEL"),
                Files      = new List<IStoredFile>()
            };

            // When the link to technical metadata is declared on the physical file element itself
            // This is always the case until the new AV workflow.
            string admId = (string) physFileElement.Attribute("ADMID");
            if (admId.HasText())
            {
                physicalFile.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
            }
            else
            {
                // TODO: this is temporary code to get round Intranda naming the attribute incorrectly
                // it can be removed later
                string amdId = (string)physFileElement.Attribute("AMDID");
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
                    Id = (string) pointer.Attribute("FILEID"),
                    WorkStore = workStore,
                    PhysicalFile = physicalFile
                };
                physicalFile.Files.Add(file);

                var fileElement = fileMap[file.Id];
                file.Use = fileElement.Parent.Attribute("USE").Value;
                admId = (string) fileElement.Attribute("ADMID");
                if (admId.HasText())
                {
                    file.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
                }
                file.RelativePath = GetLinkRef(fileElement);
                file.StorageIdentifier = GetSafeStorageIdentifier(file.RelativePath, workStore);
                file.MimeType = (string)fileElement.Attribute("MIMETYPE");
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
                        physicalFile.MimeType = file.MimeType;
                        physicalFile.RelativePath = file.RelativePath;
                        physicalFile.Family = file.Family;
                        break;
                }
            }

            return physicalFile;
        }

        private static string GetLinkRef(XElement fileElement)
        {
            var xElement = fileElement.Element(XNames.MetsFLocat);
            string linkHref = null;
            var xAttribute = xElement?.Attribute(XNames.XLinkHref);
            if (xAttribute != null)
                linkHref = xAttribute.Value;
            if (!linkHref.HasText())
                linkHref = null;
            return linkHref;
        }

        private static string GetSafeStorageIdentifier(string fullPath, IWorkStore workStore)
        {
            const string objectsPart = "objects/";
            const int pos = 8;
            string storageIdentifier;
            if (fullPath.StartsWith(objectsPart))
            {
                storageIdentifier = fullPath.Substring(pos).Replace('/', '_');
            }
            else
            {
                storageIdentifier = PathStringUtils.GetSimpleNameFromPath(fullPath);
            }
            if (!storageIdentifier.StartsWith(workStore.Identifier, StringComparison.InvariantCultureIgnoreCase))
            {
                storageIdentifier = string.Format("{0}_{1}", workStore.Identifier, storageIdentifier);
            }
            return storageIdentifier;
        }


        public IWorkStore WorkStore { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public int? Order { get; set; }
        public int Index { get; set; }
        public string OrderLabel { get; set; }

        // we need to replace SdbId with StorageIdentifier
        //public Guid SdbId { get; set; }
        public string StorageIdentifier { get; set; }

        public string MimeType { get; set; }
        public IAssetMetadata AssetMetadata { get; set; }

        // These two fields are obtained from MODS
        public string AccessCondition { get; set; }
        public string DzLicenseCode { get; set; }

        public List<IStoredFile> Files { get; set; }
        
        public string RelativePath { get; set; }
        public string RelativeAltoPath { get; set; }
        public string RelativePosterPath { get; set; }
        public string RelativeTranscriptPath { get; set; }
        public string RelativeMasterPath { get; set; }

        public AssetFamily Family { get; set; }

        public IArchiveStorageStoredFileInfo GetStoredFileInfo()
        {
            return WorkStore.GetFileInfoForPath(RelativePath);
        }

        public IArchiveStorageStoredFileInfo GetStoredAltoFileInfo()
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

        public string ToStringWithDimensions()
        {
            return ToString() + " |  w=" + AssetMetadata.GetImageWidth() + ",h=" + AssetMetadata.GetImageHeight();
        }

        /// <summary>
        /// Gather all the mets:file elements and store them by ID attribute to avoid repeated traversal
        /// </summary>
        /// <param name="rootElement"></param>
        /// <returns></returns>
        public static Dictionary<string, XElement> MakeFileMap(XElement rootElement)
        {
            return rootElement
                .Element(XNames.MetsFileSec)
                ?.Descendants(XNames.MetsFile)
                .ToDictionary(file => (string) file.Attribute("ID"));
        }
    }
    
}
