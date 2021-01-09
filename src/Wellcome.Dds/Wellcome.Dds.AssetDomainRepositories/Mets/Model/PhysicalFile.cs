using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Amazon.Runtime.Internal;
using Utils;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PhysicalFile : IPhysicalFile
    {
        public PhysicalFile(XElement physFileElement, Dictionary<string, XElement> fileMap, IWorkStore workStore)
        {
            WorkStore = workStore;
            var metsRoot = physFileElement.AncestorsAndSelf().Last();
            Id =         (string) physFileElement.Attribute("ID");
            Type =       (string) physFileElement.Attribute("TYPE");
            Order =      (int?)   physFileElement.Attribute("ORDER");
            OrderLabel = (string) physFileElement.Attribute("ORDERLABEL");
            Files =      new List<IStoredFile>();
            
            // When the link to technical metadata is declared on the physical file element itself
            // This is always the case until the new AV workflow.
            string admId = (string) physFileElement.Attribute("ADMID");
            if (admId.HasText())
            {
                AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
            }
            else
            {
                // TODO: this is temporary code to get round Intranda naming the attribute incorrectly
                // it can be removed later
                string amdId = (string)physFileElement.Attribute("AMDID");
                if (amdId.HasText())
                {
                    AssetMetadata = workStore.MakeAssetMetadata(metsRoot, amdId);
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
                    PhysicalFile = this
                };
                Files.Add(file);

                var fileElement = fileMap[file.Id];
                file.Use = fileElement.Parent.Attribute("USE").Value;
                if (file.Use == "OBJECTS")
                {
                    // Will be almost everything, before MXF workflow.
                    file.Use = "ACCESS";
                }
                var xElement = fileElement.Element(XNames.MetsFLocat);
                string linkHref = null;
                var xAttribute = xElement?.Attribute(XNames.XLinkHref);
                if (xAttribute != null)
                    linkHref = xAttribute.Value;
                if (!linkHref.HasText())
                    linkHref = null;
                admId = (string) fileElement.Attribute("ADMID");
                if (admId.HasText())
                {
                    file.AssetMetadata = workStore.MakeAssetMetadata(metsRoot, admId);
                }
                file.StorageIdentifier = GetSafeStorageIdentifier(linkHref);
                file.MimeType = (string)fileElement.Attribute("MIMETYPE");
                file.RelativePath = linkHref;
                file.Family = file.MimeType.GetAssetFamily();

                switch (file.Use)
                {
                    case "ALTO":
                        RelativeAltoPath = linkHref; 
                        break;
                    
                    case "POSTER":
                        RelativePosterPath = linkHref;
                        break;
                    
                    case "PRESERVATION":
                        RelativeMasterPath = linkHref;
                        break;
                    
                    case "TRANSCRIPT":
                        RelativeTranscriptPath = linkHref;
                        break;
                        
                    default:
                        // Assume that anything else (it should be ACCESS) is the access version,
                        // and the source of the physical file properties.
                        AssetMetadata ??= file.AssetMetadata;
                        StorageIdentifier = file.StorageIdentifier;
                        MimeType = file.MimeType;
                        RelativePath = file.RelativePath;
                        Family = file.Family;
                        break;
                }
            }
        }

        private string GetSafeStorageIdentifier(string fullPath)
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
            if (!storageIdentifier.StartsWith(WorkStore.Identifier, StringComparison.InvariantCultureIgnoreCase))
            {
                storageIdentifier = string.Format("{0}_{1}", WorkStore.Identifier, storageIdentifier);
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

    }
}
