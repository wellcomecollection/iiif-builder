using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using Xunit;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.Repositories.Tests.Presentation.SpecialState
{
    public class VolumeAndCopyLabelTests
    {
        private const string Title = "Title";

        private static string ManifestId(string identifier) => $"https://test/presentation/{identifier}";

        /// <summary>
        /// Build results as BuildAllManifestations leaves them: the package collection, whose items are
        /// labelled from the anchor METS order, followed by each volume's manifest.
        /// </summary>
        private static MultipleBuildResult MakeBuildResults(string package, params (string id, int order)[] volumes)
        {
            var results = new MultipleBuildResult(package);
            results.Add(new BuildResult(package, Version.V3)
            {
                IIIFResource = new Collection
                {
                    Id = $"https://test/presentation/collections/{package}",
                    Items = volumes.Select(v => (ICollectionItem) new Manifest
                    {
                        Id = ManifestId(v.id),
                        Label = new LanguageMap { ["en"] = new() { Title, $"Volume {v.order}" } }
                    }).ToList()
                }
            });
            foreach (var (id, _) in volumes)
            {
                results.Add(new BuildResult(id, Version.V3)
                {
                    IIIFResource = new Manifest
                    {
                        Id = ManifestId(id),
                        Label = new LanguageMap { ["en"] = new() { Title } }
                    }
                });
            }
            return results;
        }

        private static Dictionary<string, string> CollectionLabels(MultipleBuildResult results) =>
            ((Collection) results.First().IIIFResource!).Items!
                .OfType<Manifest>()
                .ToDictionary(m => m.Id!.Split('/').Last(), m => m.Label!["en"].Last());

        [Fact]
        public void MultiVolume_Labels_Come_From_Mods_Not_Anchor_Order()
        {
            // WSUPP-8: volume 8 in two parts, ordered 8 and 9 in the anchor METS
            var results = MakeBuildResults("b30529189",
                ("b30529189_0001", 1), ("b30529189_0003", 3), ("b30529189_0008", 8), ("b30529189_0009", 9));
            var state = new State { MultiVolumeState = new MultiVolumeState() };
            state.MultiVolumeState.VolumeNumbers["b30529189_0001"] = 1;
            state.MultiVolumeState.VolumeNumbers["b30529189_0003"] = 3;
            state.MultiVolumeState.VolumeNumbers["b30529189_0008"] = 8;
            state.MultiVolumeState.VolumeNumbers["b30529189_0009"] = 8;

            MultiVolumeState.ProcessState(results, state);

            CollectionLabels(results).Should().Equal(new Dictionary<string, string>
            {
                ["b30529189_0001"] = "Volume 1",
                ["b30529189_0003"] = "Volume 3",
                ["b30529189_0008"] = "Volume 8",
                ["b30529189_0009"] = "Volume 8"
            });
        }

        [Fact]
        public void MultiVolume_Keeps_Order_Label_When_Mods_Has_No_Volume()
        {
            var results = MakeBuildResults("b1", ("b1_0001", 1), ("b1_0002", 2));
            var state = new State { MultiVolumeState = new MultiVolumeState() };
            state.MultiVolumeState.VolumeNumbers["b1_0002"] = 5;

            MultiVolumeState.ProcessState(results, state);

            CollectionLabels(results).Should().Equal(new Dictionary<string, string>
            {
                ["b1_0001"] = "Volume 1",
                ["b1_0002"] = "Volume 5"
            });
        }

        [Fact]
        public void MultiVolume_Ignores_Single_Manifestation()
        {
            var results = new MultipleBuildResult("b1");
            results.Add(new BuildResult("b1", Version.V3) { IIIFResource = new Manifest { Id = ManifestId("b1") } });
            var state = new State { MultiVolumeState = new MultiVolumeState() };
            state.MultiVolumeState.VolumeNumbers["b1"] = 1;

            var action = () => MultiVolumeState.ProcessState(results, state);

            action.Should().NotThrow();
        }

        private static State MultiCopy(params (string id, int copy, int volume)[] items)
        {
            var state = new State { MultiCopyState = new MultiCopyState() };
            foreach (var (id, copy, volume) in items)
            {
                state.MultiCopyState.CopyAndVolumes[id] = new CopyAndVolume(id) { CopyNumber = copy, VolumeNumber = volume };
            }
            return state;
        }

        [Fact]
        public void MultiCopy_Single_Copy_Of_Single_Volume_Shows_Volume()
        {
            // WSUPP-4: we only have volume 2 of copy 1
            var results = MakeBuildResults("b29325705", ("b29325705_0002", 1));
            var state = MultiCopy(("b29325705_0002", 1, 2));

            MultiCopyState.ProcessState(results, state);

            CollectionLabels(results).Should().Equal(new Dictionary<string, string>
            {
                ["b29325705_0002"] = "Copy 1, Volume 2"
            });
        }

        [Fact]
        public void MultiCopy_Without_Volumes_Shows_Copy_Only()
        {
            var results = MakeBuildResults("b10727000", ("b10727000_0001", 1), ("b10727000_0002", 2));
            var state = MultiCopy(("b10727000_0001", 1, -1), ("b10727000_0002", 2, -1));

            MultiCopyState.ProcessState(results, state);

            CollectionLabels(results).Should().Equal(new Dictionary<string, string>
            {
                ["b10727000_0001"] = "Copy 1",
                ["b10727000_0002"] = "Copy 2"
            });
        }

        [Fact]
        public void MultiCopy_With_Multiple_Volumes_Nests_Every_Copy()
        {
            // WSUPP-4: copy 2 has only one volume, but copy 1 has two
            var results = MakeBuildResults("b30527399",
                ("b30527399_0001", 1), ("b30527399_0002", 2), ("b30527399_0003", 3));
            var state = MultiCopy(
                ("b30527399_0001", 1, 1), ("b30527399_0002", 1, 2), ("b30527399_0003", 2, 1));

            MultiCopyState.ProcessState(results, state);

            var copyCollections = ((Collection) results.First().IIIFResource!).Items!.Cast<Collection>().ToList();
            copyCollections.Select(c => c.Label!["en"].Single()).Should().Equal("Copy 1", "Copy 2");
            copyCollections.SelectMany(c => c.Items!.Cast<Manifest>())
                .Select(m => m.Label!["en"].Last())
                .Should().Equal("Copy 1, Volume 1", "Copy 1, Volume 2", "Copy 2, Volume 1");
        }
    }
}
