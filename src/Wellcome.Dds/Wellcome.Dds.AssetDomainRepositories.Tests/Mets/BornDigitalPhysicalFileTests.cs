using System.Xml.Linq;
using FakeItEasy;
using FluentAssertions;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class BornDigitalPhysicalFileTests
    {
        private static readonly XNamespace Mets = "http://www.loc.gov/METS/";
        private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

        private static IPhysicalFile FromHref(string href)
        {
            var fileElement = new XElement(Mets + "file",
                new XAttribute("ID", "file-48b6105b"),
                new XAttribute("ADMID", "amdSec_8"),
                new XElement(Mets + "FLocat", new XAttribute(XLink + "href", href)));
            var root = new XElement(Mets + "mets",
                new XElement(Mets + "fileGrp", new XAttribute("USE", "original"), fileElement));

            var workStore = A.Fake<IWorkStore>();
            A.CallTo(() => workStore.PackageIdentifier).Returns("PPHTH/B/2/89/8/11");
            var assetMetadata = A.Fake<IAssetMetadata>();
            var rights = A.Fake<IRightsStatement>();
            A.CallTo(() => rights.AccessCondition).Returns("Open");
            A.CallTo(() => assetMetadata.GetRightsStatement()).Returns(rights);
            A.CallTo(() => assetMetadata.GetMimeType()).Returns("application/msword");
            A.CallTo(() => workStore.MakeAssetMetadata(root, "amdSec_8")).Returns(assetMetadata);

            return PhysicalFile.FromBornDigitalMets(root, fileElement, workStore);
        }

        [Fact]
        public void Percent_Encoded_Parentheses_Are_Decoded()
        {
            // WSUPP-45: as written in the original fileGrp of PPHTH_B_2_89_8_11
            var physicalFile = FromHref("objects/Facilitator_s_time_recording_sheet_%28DRAFT%29.doc");

            physicalFile.RelativePath.Should().Be("objects/Facilitator_s_time_recording_sheet_(DRAFT).doc");
            physicalFile.StorageIdentifier.Should()
                .Be("PPHTH_B_2_89_8_11---Facilitator_s_time_recording_sheet_(DRAFT).doc");
            physicalFile.Files![0].RelativePath.Should().Be(physicalFile.RelativePath);
            physicalFile.Files[0].StorageIdentifier.Should().Be(physicalFile.StorageIdentifier);
        }

        [Fact]
        public void Other_Characters_Are_Left_Alone()
        {
            var physicalFile = FromHref("objects/Sub folder/100%2C_not_an_escape %41.doc");

            physicalFile.RelativePath.Should().Be("objects/Sub folder/100%2C_not_an_escape %41.doc");
            physicalFile.StorageIdentifier.Should()
                .Be("PPHTH_B_2_89_8_11---Sub_folder---100%2C_not_an_escape_%41.doc");
        }
    }
}
