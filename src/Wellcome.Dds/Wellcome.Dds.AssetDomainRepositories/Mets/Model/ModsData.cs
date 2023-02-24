using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    /// <summary>
    /// MODS data is used in Goobi-origin METS files.
    /// It contains access conditions and rights information, as well as
    /// additional information specific to digitised resources.
    /// </summary>
    [Serializable]
    public class ModsData : ISectionMetadata
    {
        public string? Title { get; set; }
        public string? SubTitle { get; set; }
        public string? DisplayDate { get; set; }
        public string? OriginPublisher { get; set; }
        public string? RecordIdentifier { get; set; }
        public string? AccessCondition { get; set; }
        public string? DzLicenseCode { get; set; }
        public int PlayerOptions { get; set; }
        public string? Usage { get; set; }
        public string? Leader6 { get; set; }
        public int VolumeNumber { get; set; }
        public int CopyNumber { get; set; }
        
        // Used by Chemist and Druggist (Periodical) for volume and issue numbers
        public string? Number { get; set; }
        public int PartOrder { get; set; }

        public ModsData(XElement dmdSec)
        {
            var modsEl = dmdSec.GetSingleElementWithAttribute(XNames.MetsMdWrap, "MDTYPE", "MODS");
            var xmlData = modsEl.Element(XNames.MetsXmlData);
            Debug.Assert(xmlData != null, "xmlData != null");
            var modsDoc = new XDocument(xmlData.FirstNode);
            if (modsDoc.Root == null)
            {
                return;
            }

            // <mods:note type="noteType">value</mods:note>
            Leader6 = ExtractSingleModsNoteField(modsDoc, "leader6");
            Title = HtmlUtils.TextOnly(modsDoc.GetDescendantElementValue(XNames.ModsTitle));
            SubTitle = HtmlUtils.TextOnly(modsDoc.GetDescendantElementValue(XNames.ModsSubTitle));
            OriginPublisher = modsDoc.GetDescendantElementValue(XNames.ModsOriginPublisher);
            if (!OriginPublisher.HasText())
            {
                OriginPublisher = modsDoc.GetDescendantElementValue(XNames.ModsPublisher);
            }
            DisplayDate = GetCleanDisplayDate(modsDoc);
            RecordIdentifier = modsDoc.GetDescendantElementValue(XNames.ModsRecordIdentifier);
            var accessConditions = modsDoc.Root.Elements(XNames.ModsAccessCondition).ToList();

            var statusAccessConditionElements = accessConditions
                .Where(x => (string?) x.Attribute("type") == "status").ToList();
            if (statusAccessConditionElements.Count > 1)
            {
                throw new NotSupportedException("METS file contains more than one accessCondition of type 'status'");
            }
            
            if (statusAccessConditionElements.Any())
            {
                string accessConditionValue = statusAccessConditionElements.First().Value;
                if (Common.AccessCondition.IsValid(accessConditionValue))
                {
                    AccessCondition = accessConditionValue;
                }
            }
            if (!AccessCondition.HasText())
            {
                AccessCondition = Common.AccessCondition.Open;
            }
            // TODO: revisit this behaviour for https://github.com/wellcomecollection/platform/issues/5619
            // e.g., AccessCondition = Common.AccessCondition.Missing;

            var dzAccessConditionElements = accessConditions
                .Where(x => (string?) x.Attribute("type") == "dz").ToList();
            if (dzAccessConditionElements.Count > 1)
            {
                throw new NotSupportedException("METS file contains more than one accessCondition of type 'dz' (more than one license code)");
            }
            if (dzAccessConditionElements.Any())
            {
                DzLicenseCode = dzAccessConditionElements.First().Value;
            }

            var playerOptionsElements = accessConditions
                .Where(x => (string?) x.Attribute("type") == "player").ToList();
            if (playerOptionsElements.Count > 1)
            {
                throw new NotSupportedException("METS file contains more than one accessCondition of type 'player'");
            }
            if (playerOptionsElements.Any())
            {
                PlayerOptions = Convert.ToInt32(playerOptionsElements.First().Value);
            }

            var usageElements = accessConditions
                .Where(x => (string?) x.Attribute("type") == "usage").ToList();
            if (usageElements.Count > 1)
            {
                throw new NotSupportedException("METS file contains more than one accessCondition of type 'usage'");
            }
            if (usageElements.Any())
            {
                // so where does this parse method go?
                // needs to replace bare license with link
                // then find its way into the attribution field
                // Usage = ParseUsage(usageElement.Value);

                Usage = usageElements.First().Value;
            }

            SetCopyAndVolumeNumbers(modsDoc, this);
            
            // Additions for Chemist and Druggist
            var part = modsDoc.Root.Elements(XNames.ModsPart).FirstOrDefault();
            if (part != null)
            {
                var order = part.GetAttributeValue("order", null);
                if (order.HasText())
                {
                    if (int.TryParse(order, out var orderInt))
                    {
                        PartOrder = orderInt;
                    }
                }
            }
            Number = modsDoc.GetDescendantElementValue(XNames.ModsNumber);
        }

        private string? ExtractSingleModsNoteField(XDocument modsDoc, string noteType)
        {
            var noteEl = modsDoc.Root!
                .Descendants(XNames.ModsNote)
                .SingleOrDefault(x => (string?) x.Attribute("type") == noteType);
            return noteEl?.Value;
        }

        private static string? GetCleanDisplayDate(XDocument modsDoc)
        {
            string? displayDate = null;
            int cutoffYear = DateTime.Now.AddYears(10).Year;
            try
            {
                foreach (var elementValue in modsDoc.GetDescendantElementValues(XNames.ModsDateIssued))
                {
                    if (elementValue.HasText())
                    {
                        if (elementValue.Length == 4)
                        {
                            if (Int32.TryParse(elementValue, out var y))
                            {
                                if (y > cutoffYear)
                                    continue;
                            }
                        }
                        displayDate = elementValue;
                        break;
                    }
                }
            }
            catch
            {
                // log
            }
            return displayDate;
        }

        private void SetCopyAndVolumeNumbers(XDocument modsDoc, ModsData modsData)
        {
            var volumeNumber = modsDoc.GetDescendantElementValue(XNames.WtVolumeNumber);
            if (volumeNumber.HasText())
            {
                modsData.VolumeNumber = Convert.ToInt32(volumeNumber);
            }
            else
            {
                modsData.VolumeNumber = -1;
            }
            var copyNumber = modsDoc.GetDescendantElementValue(XNames.WtCopyNumber);
            if (copyNumber.HasText())
            {
                modsData.CopyNumber = Convert.ToInt32(copyNumber);
            }
            else
            {
                // CHANGE from old DDS - I'm setting this to -1 to indicate that it was not present in the METS
                // This now matches the Volume number behaviour
                modsData.CopyNumber = -1;
                // modsData.CopyNumber = 1;
            }
        }

        public string GetDisplayTitle()
        {
            return "" + Title + SubTitle;
        }
    }
}
