using System;
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
        public PhysicalFile(XElement physFileElement, Dictionary<string, XElement> fileMap, IWorkStore workStore)
        {
            WorkStore = workStore;
            var metsRoot = physFileElement.AncestorsAndSelf().Last();
            Id =         (string) physFileElement.Attribute("ID");
            Type =       (string) physFileElement.Attribute("TYPE");
            Order =      (int?)   physFileElement.Attribute("ORDER");
            OrderLabel = (string) physFileElement.Attribute("ORDERLABEL");

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
            
            var filePointers = physFileElement.Elements(XNames.MetsFptr);
            foreach (var pointer in filePointers)
            {
                var fileId = (string) pointer.Attribute("FILEID");
                var fileElement = fileMap[fileId];
                var xElement = fileElement.Element(XNames.MetsFLocat);
                string linkHref = null;
                if (xElement != null)
                {
                    var xAttribute = xElement.Attribute(XNames.XLinkHref);
                    if (xAttribute != null)
                        linkHref = xAttribute.Value;
                }

                if (fileId.EndsWith("_SDB"))  
                {
                    // Preservica version, with GUIDs
                    StorageIdentifier = (string) pointer.Attribute("CONTENTIDS");
                    //SdbId = (Guid) pointer.Attribute("CONTENTIDS");
                    MimeType = (string) fileElement.Attribute("MIMETYPE");
                    RelativePath = linkHref;
                    Family = MimeType.GetAssetFamily();
                }
                else if (fileId.EndsWith("_OBJECTS"))
                {
                    // This avoids mapping into the Premis metadata just to get the new identifier
                    // but, how safe is it?
                    StorageIdentifier = GetSafeStorageIdentifier(linkHref); 
                    //SdbId = Guid.Empty;
                    MimeType = (string)fileElement.Attribute("MIMETYPE");
                    RelativePath = linkHref;
                    Family = MimeType.GetAssetFamily();
                }
                else if (fileId.EndsWith("_ALTO"))
                {
                    if (linkHref.HasText())
                    {
                        RelativeAltoPath = linkHref; // TODO - RelativeAltoPath should be IStoredFileInfo
                    }
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

        public string RelativePath { get; set; }
        public string RelativeAltoPath { get; set; }

        public AssetFamily Family { get; set; }

        public IStoredFileInfo GetStoredFileInfo()
        {
            return WorkStore.GetFileInfoForPath(RelativePath);
        }

        public IStoredFileInfo GetStoredAltoFileInfo()
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
