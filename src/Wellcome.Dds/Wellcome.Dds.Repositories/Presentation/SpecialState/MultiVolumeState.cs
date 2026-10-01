using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// Multi-volume items label each volume in the parent collection with its volume number.
    /// The parent collection is built from the anchor METS, which only gives each volume's ORDER;
    /// the volume number is in each volume's own MODS, so the labels are corrected once all the
    /// volumes have been built. The two differ when, for example, a volume in two parts has both
    /// parts catalogued as the same volume, e.g. b30529189 (JIRA WSUPP-8).
    /// </summary>
    public class MultiVolumeState
    {
        public readonly Dictionary<string, int> VolumeNumbers = new Dictionary<string, int>();

        public static void ProcessState(MultipleBuildResult buildResults, State state)
        {
            if (buildResults.First().IIIFResource is not Collection bNumberCollection)
            {
                // A single manifestation with a volume number; there's no collection to label.
                return;
            }

            foreach (var (identifier, volumeNumber) in state.MultiVolumeState!.VolumeNumbers)
            {
                var manifestId = (buildResults.SingleOrDefault(br => br.Id == identifier)?.IIIFResource as Manifest)?.Id;
                if (manifestId == null)
                {
                    // The volume failed to build
                    continue;
                }
                var collectionItem = bNumberCollection.Items?
                    .OfType<Manifest>()
                    .SingleOrDefault(m => m.Id == manifestId);
                if (collectionItem?.Label == null)
                {
                    continue;
                }
                foreach (var values in collectionItem.Label.Values)
                {
                    var index = values.FindIndex(v => v.StartsWith("Volume "));
                    if (index >= 0)
                    {
                        values[index] = $"Volume {volumeNumber}";
                    }
                }
            }
        }
    }
}
