﻿#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.IIIFBuilding
{
    public class BuildResult
    {
        public BuildResult(string id, Version version)
        {
            Id = id;
            IIIFVersion = version;
        }
        
        public bool MayBeConvertedToV2 => TimeBasedCount <= 0 && FileCount <= 0;

        public string Id { get; set; }
        
        public bool RequiresMultipleBuild { get; set; }
        
        /// <summary>
        /// Overall outcome of build operation
        /// </summary>
        public BuildOutcome Outcome { get; set; }
        
        public string? Message { get; set; }
        
        /// <summary>
        /// The IIIF Presentation Version this BuildResult represents.
        /// </summary>
        public Version IIIFVersion { get; }
        
        /// <summary>
        /// Generated IIIF result
        /// </summary>
        public JsonLdBase? IIIFResource { get; set; }

        // Track what kinds of assets we are building Manifests for
        public int ImageCount { get; set; } = 0;
        public int TimeBasedCount { get; set; } = 0;
        public int FileCount { get; set; } = 0;

        /// <summary>
        /// Get storage key where this build result would be stored.
        /// </summary>
        public string GetStorageKey()
            => IIIFVersion switch
            {
                Version.V2 => $"v2/{Id}",
                Version.V3 => $"v3/{Id}",
                Version.Unknown => throw new InvalidOperationException(
                    "Unable to get storage get for Unknown IIIFVersion"),
                _ => throw new ArgumentOutOfRangeException()
            };
    }

    public class MultipleBuildResult : IEnumerable<BuildResult>
    {
        public MultipleBuildResult(string identifier)
        {
            Identifier = identifier;
        }
        
        /// <summary>
        /// The package identifier
        /// </summary>
        public string Identifier { get; set; }
        
        private readonly Dictionary<string, BuildResult> resultDict = new();
        private readonly List<string> buildOrder = new();

        public bool MayBeConvertedToV2
        {
            get
            {
                return this.All(buildResult => buildResult.MayBeConvertedToV2);
            }
        }

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

        public BuildResult? this[string id] => resultDict.TryGetValue(id, out var result) ? result : null;

        public BuildOutcome Outcome { get; set; }
        public string? Message { get; set; }

        public IEnumerator<BuildResult> GetEnumerator()
        {
            foreach (var id in buildOrder)
            {
                yield return resultDict[id];
            }
        }
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void RemoveAll()
        {
            resultDict.Clear();
            buildOrder.Clear();
        }
    }
}
