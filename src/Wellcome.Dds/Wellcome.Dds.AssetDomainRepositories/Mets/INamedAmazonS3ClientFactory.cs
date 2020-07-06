using Amazon.S3;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    /// <summary>
    /// Interface to support managing named <see cref="IAmazonS3"/> clients.
    /// </summary>
    public interface INamedAmazonS3ClientFactory
    {
        /// <summary>
        /// Get a previously registered named IAmazonS3 client.
        /// </summary>
        /// <param name="name">Name of client to fetch.</param>
        /// <returns>IAmazonS3 client.</returns>
        IAmazonS3 Get(string name);
        
        /// <summary>
        /// Add a named IAmazonS3Client.
        /// </summary>
        /// <param name="name">Name of client.</param>
        /// <param name="client">IAmazonS3Client implementation.</param>
        void Add(string name, IAmazonS3 client);
    }
}