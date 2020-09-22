using System.Collections;
using System.Collections.Generic;
using IIIF.Presentation;

namespace Wellcome.Dds.IIIFBuilding
{
    public class BuildResult
    {
        public BuildResult(string id)
        {
            Id = id;
        }
        
        public string Id { get; }
        public bool RequiresMultipleBuild { get; set; }
        public BuildOutcome Outcome { get; set; }
        public string Message { get; set; }
        public string IIIF3Key { get; set; }
        public StructureBase IIIF3Resource { get; set; }
        
        
        // TODO - this won't be a StructureBase
        public string IIIF2Key { get; set; }
        public StructureBase IIIF2Resource { get; set; }
        
    }

    public class MultipleBuildResult : IEnumerable<BuildResult>
    {
        // The b number
        public string Identifier { get; set; }
        
        private readonly Dictionary<string, BuildResult> resultDict = new Dictionary<string, BuildResult>();
        private readonly List<string> buildOrder = new List<string>();

        public void Add(BuildResult buildResult)
        {
            buildOrder.Add(buildResult.Id);
            resultDict[buildResult.Id] = buildResult;
        }

        public void Remove(string id)
        {
            buildOrder.Remove(id);
            resultDict.Remove(id);
        }

        public int Count => buildOrder.Count;

        public BuildResult this[string id] => resultDict.TryGetValue(id, out var result) ? result : null;

        public BuildOutcome Outcome { get; set; }
        public string Message { get; set; }

        public IEnumerator<BuildResult> GetEnumerator()
        {
            foreach (var id in buildOrder)
            {
                yield return resultDict[id];
            }
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
