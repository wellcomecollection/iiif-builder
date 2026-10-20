using System;
using System.Threading.Tasks;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// The outcome of asking the storage service for one (space, externalIdentifier) bag.
    /// </summary>
    public class StorageProbeResult
    {
        public StorageProbeResult(string storageSpace, string identifier)
        {
            StorageSpace = storageSpace;
            Identifier = identifier;
        }

        public string StorageSpace { get; }

        /// <summary>
        /// Exactly what was sent. The storage service is case-sensitive.
        /// </summary>
        public string Identifier { get; }

        public bool Found { get; set; }

        /// <summary>
        /// The HTTP status when the bag was not returned; 404 is the normal miss.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Set when the probe failed for a reason other than a 404.
        /// </summary>
        public string? Error { get; set; }

        public string? Version { get; set; }
        public string? Bucket { get; set; }
        public string? Path { get; set; }
        public DateTime? Created { get; set; }

        public bool NotFound => !Found && StatusCode == 404;
    }

    /// <summary>
    /// A single storage service lookup with no retries and no caching across spaces, for probing.
    /// </summary>
    public interface IStorageServiceProbe
    {
        /// <summary>
        /// Which storage service this probes, e.g. the bags API template, for display.
        /// </summary>
        string StorageApi { get; }

        Task<StorageProbeResult> Probe(string storageSpace, string identifier);
    }
}
