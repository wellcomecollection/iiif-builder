using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Utils;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    public class CopyAndVolume
    {
        public CopyAndVolume(string id)
        {
            Id = id;
        }
        public string Id { get; set; }
        public int CopyNumber { get; set; }
        public int VolumeNumber { get; set; }
    }
    
    /// <summary>
    /// Multi-copy items are items with multiple copies of the same work, e.g.
    /// b10727000
    /// b17523588
    /// b10003757
    /// </summary>
    public class MultiCopyState
    {
        public readonly Dictionary<string, CopyAndVolume> CopyAndVolumes = new Dictionary<string, CopyAndVolume>();

        public static void ProcessState(MultipleBuildResult buildResults, State state)
        {
            // We should have ended up with a Collection, comprising more than one Manifest.
            // Each Manifest will have a copy number.
            // There might be more than one volume per copy, in which case we have
            // a nested collection.
            if (buildResults.First().IIIFResource is not Collection bNumberCollection)
            {
                throw new IIIFBuildStateException("State is missing the parent collection");
            }

            var newItems = bNumberCollection.Items = new List<ICollectionItem>();
            var copies = state.MultiCopyState!.CopyAndVolumes.Values
                .Select(cv => cv.CopyNumber)
                .Distinct().ToList(); // leave in the order we find them
            foreach (int copy in copies)
            {
                var volumesForCopy = state.MultiCopyState.CopyAndVolumes.Values
                    .Where(cv => cv.CopyNumber == copy)
                    .Select(cv => cv.VolumeNumber)
                    .ToList();
                if (volumesForCopy.Count > 1)
                {
                    // This volume is a collection child of the root;
                    // create the copy collection then add its volumes
                    var copyCollection = new Collection
                    {
                        Id = $"{bNumberCollection.Id}-copy-{copy}",
                        Label = Lang.Map($"Copy {copy}"),
                        Items = new List<ICollectionItem>()
                    };
                    newItems.Add(copyCollection);
                    foreach (int volume in volumesForCopy)
                    {
                        var identifiersForVolume =
                            state.MultiCopyState.CopyAndVolumes.Values
                                .Where(cv => cv.CopyNumber == copy && cv.VolumeNumber == volume);
                        foreach (var copyAndVolume in identifiersForVolume)
                        {
                            var manifestResult = buildResults.Single(br => br.Id == copyAndVolume.Id);
                            var manifest = (Manifest?) manifestResult.IIIFResource;
                            manifest!.Label!.Values.First().Add($"Copy {copy}, Volume {volume}");
                            copyCollection.Items.Add(new Manifest
                            {
                                Id = manifest.Id,
                                Label = manifest.Label,
                                Thumbnail = manifest.Thumbnail
                            });
                            if (manifest.Thumbnail.HasItems())
                            {
                                copyCollection.Thumbnail ??= new List<ExternalResource>();
                                copyCollection.Thumbnail.AddRange(manifest.Thumbnail);
                            }
                        }
                    }
                }
                else
                {
                    // This copy is attached to the b-number collection directly
                    // accept multiple copies with the same number, just in case
                    var identifiersForCopy =
                        state.MultiCopyState.CopyAndVolumes.Values.Where(cv => cv.CopyNumber == copy);
                    foreach (var copyAndVolume in identifiersForCopy)
                    {
                        var manifestResult = buildResults.Single(br => br.Id == copyAndVolume.Id);
                        var manifest = (Manifest?) manifestResult.IIIFResource;
                        manifest!.Label!.Values.First().Add($"Copy {copy}");
                        newItems.Add(new Manifest
                        {
                            Id = manifest.Id,
                            Label = manifest.Label,
                            Thumbnail = manifest.Thumbnail
                        });
                    }
                }
            }
        }
    }
}