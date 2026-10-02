using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using FluentAssertions;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation;
using Xunit;
using Range = IIIF.Presentation.V3.Range;

namespace Wellcome.Dds.Repositories.Tests.Presentation
{
    public class StructuresTests
    {
        private readonly IIIFBuilderParts sut = new(
            A.Fake<IDigitalObjectRepository>(),
            new UriPatterns(Options.Create(new DdsOptions
            {
                LinkedDataDomain = "https://test",
                WellcomeCollectionApi = "https://test.api",
                ApiWorkTemplate = "https://test.api/works"
            })),
            "https://dlcs",
            false,
            Array.Empty<string>());

        private static StructRange Directory(string id, string label, string[] files, params StructRange[] children) =>
            new()
            {
                Id = id,
                Label = label,
                Type = "Directory",
                PhysicalFileIds = files.ToList(),
                Children = children.Length > 0 ? children.Cast<IStructRange>().ToList() : null,
                SectionMetadata = new BornDigitalSectionMetadata { Title = label }
            };

        private static IManifestation MakeManifestation(string type, StructRange root)
        {
            var manifestation = A.Fake<IManifestation>();
            A.CallTo(() => manifestation.Identifier).Returns("PPFDN/E/2");
            A.CallTo(() => manifestation.Type).Returns(type);
            A.CallTo(() => manifestation.RootStructRange).Returns(root);
            A.CallTo(() => manifestation.ParentSectionMetadata).Returns(null);
            var files = new List<string>();
            void Collect(IStructRange range)
            {
                files.AddRange(range.PhysicalFileIds!);
                range.Children?.ForEach(Collect);
            }
            Collect(root);
            // In Goobi METS a file can be in more than one range, but it's in the sequence once
            manifestation.Sequence = files.Distinct().Select(id =>
            {
                var file = A.Fake<IPhysicalFile>();
                file.Id = id;
                file.StorageIdentifier = $"PPFDN_E_2---{id}";
                return file;
            }).ToList();
            return manifestation;
        }

        private static IEnumerable<string> CanvasIds(Range range) =>
            range.Items!.OfType<Canvas>().Select(c => c.Id!.Split("---").Last());

        [Fact]
        public void BornDigital_Root_Files_Are_Kept_Alongside_Folders()
        {
            // WSUPP-44: three docs in the root, beside a folder of photos
            var root = Directory(null, "objects", new[] { "a.doc", "b.doc", "c.doc" },
                Directory("FD_PHOTOS_ON_CD", "FD_PHOTOS_ON_CD", new[] { "1.tif", "2.tif" }));
            var manifest = new Manifest();

            sut.Structures(manifest, MakeManifestation("Born Digital", root));

            var structure = manifest.Structures.Should().ContainSingle().Subject;
            structure.Id.Should().Be("https://test/presentation/PPFDN/E/2/ranges/objects");
            structure.Label!["none"].Single().Should().Be("objects");
            CanvasIds(structure).Should().Equal("a.doc", "b.doc", "c.doc");
            var folder = structure.Items!.OfType<Range>().Should().ContainSingle().Subject;
            folder.Id.Should().Be("https://test/presentation/PPFDN/E/2/ranges/FD_PHOTOS_ON_CD");
            CanvasIds(folder).Should().Equal("1.tif", "2.tif");
        }

        [Fact]
        public void BornDigital_Folders_Only_Keep_Folders_At_Top_Level()
        {
            var root = Directory(null, "objects", Array.Empty<string>(),
                Directory("A", "A", new[] { "1.tif" }),
                Directory("B", "B", new[] { "2.tif" }));
            var manifest = new Manifest();

            sut.Structures(manifest, MakeManifestation("Born Digital", root));

            manifest.Structures!.Select(r => r.Id!.Split('/').Last()).Should().Equal("A", "B");
        }

        [Fact]
        public void BornDigital_Files_Only_Have_No_Structures()
        {
            var root = Directory(null, "objects", new[] { "a.doc", "b.doc" });
            var manifest = new Manifest();

            sut.Structures(manifest, MakeManifestation("Born Digital", root));

            manifest.Structures.Should().BeNull();
        }

        [Fact]
        public void Digitised_Root_Canvases_Are_Not_Promoted()
        {
            // For Goobi METS the root is the whole manifestation, so only child structure is wanted
            var root = Directory("LOG_0000", "Monograph", new[] { "p1", "p2" },
                Directory("LOG_0001", "Chapter", new[] { "p2" }));
            var manifest = new Manifest();

            sut.Structures(manifest, MakeManifestation("Monograph", root));

            manifest.Structures!.Select(r => r.Id!.Split('/').Last()).Should().Equal("LOG_0001");
        }
    }
}
