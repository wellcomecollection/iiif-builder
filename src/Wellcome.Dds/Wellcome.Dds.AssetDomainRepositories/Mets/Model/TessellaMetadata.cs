using System;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    /// <summary>
    /// This is a fairly light wrapper over the Tessella XML in METS
    /// Lazily loaded if you don't need file info (e.g., Dashboard does not need image dimensions for aggregate ops)
    /// </summary>
    public class TessellaMetadata : IAssetMetadata
    {
        private XElement fileElement;
        private readonly XElement metsRoot;
        private readonly string admId;
        private bool initialised = false;

        public TessellaMetadata(XElement metsRoot, string admId)
        {
            // This is a lazy class indeed...
            this.metsRoot = metsRoot;
            this.admId = admId;
        }

        public string GetFileName()
        {
            if (!initialised) Init();
            return fileElement.GetDesendantElementValue(XNames.TessellaFileName);
        }
        public string GetFolder()
        {
            if (!initialised) Init();
            return fileElement.GetDesendantElementValue(XNames.TessellaFolder);
        }

        public string GetFileSize()
        {
            if (!initialised) Init();
            return fileElement.GetDesendantElementValue(XNames.TessellaFileSize);
        }
        public string GetFormatName()
        {
            if (!initialised) Init();
            return fileElement.GetDesendantElementValue(XNames.TessellaFormatName);
        }

        public string GetFormatVersion()
        {
            throw new NotImplementedException();
        }

        public string GetPronomKey()
        {
            throw new NotImplementedException();
        }

        public string GetAssetId()
        {
            if (!initialised) Init();
            return fileElement.GetDesendantElementValue(XNames.TessellaID);
        }

        public string GetLengthInSeconds()
        {
            return GetFilePropertyValue("Length In Seconds");
        }

        public double GetDuration()
        {
            throw new System.NotImplementedException();
        }

        public string GetBitrateKbps()
        {
            return GetFilePropertyValue("Bitrate (kbps)");
        }

        public int GetNumberOfPages()
        {
            var num = GetInt32FilePropertyValue("Number of Pages");
            return num ?? 0;
        }

        public int GetNumberOfImages()
        {
            var num = GetInt32FilePropertyValue("Number of Images");
            return num ?? 0;
        }

        public int GetImageWidth()
        {
            var num = GetInt32FilePropertyValue("Image Width");
            return num ?? 0;
        }

        public int GetImageHeight()
        {
            var num = GetInt32FilePropertyValue("Image Height");
            return num ?? 0;
        }

        private string GetFilePropertyValue(string filePropertyName)
        {
            if (!initialised) Init();
            var el = fileElement.Descendants(XNames.TessellaFilePropertyName).SingleOrDefault(d => d.Value == filePropertyName);
            if (el != null)
            {
                var sib = el.ElementsAfterSelf(XNames.TessellaValue).SingleOrDefault();
                if (sib != null)
                {
                    return sib.Value;
                }
            }
            return null;
        }

        public int? GetInt32FilePropertyValue(string filePropertyName)
        {
            int i;
            var fpv = GetFilePropertyValue(filePropertyName);
            if (int.TryParse(fpv, out i))
            {
                return i;
            }
            return null;
        }

        public IRightsStatement GetRightsStatement()
        {
            throw new NotImplementedException();
        }
        
        private void Init()
        {
            var techMd = metsRoot.GetSingleDescendantWithAttribute(XNames.MetsTechMD, "ID", admId);
            var xmlData = techMd.Descendants(XNames.MetsXmlData).Single();
            fileElement = xmlData.Element(XNames.TessellaFile);
            //fileDoc = new XDocument(xmlData.FirstNode);
        }

        public string GetOriginalName()
        {
            throw new System.NotImplementedException();
        }

        public string GetMimeType()
        {
            throw new NotImplementedException();
        }

        public DateTime? GetCreatedDate()
        {
            throw new NotImplementedException();
        }
    }
}
