using Amazon.S3;

namespace Utils.Aws.S3
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
        IAmazonS3 Get(NamedClient name);
        
        /// <summary>
        /// Add a named IAmazonS3Client.
        /// </summary>
        /// <param name="name">Name of client.</param>
        /// <param name="client">IAmazonS3Client implementation.</param>
        void Add(NamedClient name, IAmazonS3 client);
    }
}