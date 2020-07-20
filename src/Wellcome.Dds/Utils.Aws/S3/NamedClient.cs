using System;

namespace Utils.Aws.S3
{
    /// <summary>
    /// Available named S3 clients
    /// </summary>
    [Flags]
    public enum NamedClient
    {
        /// <summary>
        /// S3 client for accessing DDS resources.
        /// </summary>
        Dds = 1,

        /// <summary>
        /// S3 client for accessing wellcome-storage.
        /// </summary>
        Storage = 2,

        /// <summary>
        /// S3 client for all available.
        /// </summary>
        All = Dds | Storage
    }
}
    