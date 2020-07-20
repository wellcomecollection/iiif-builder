using System.Collections.Generic;
using Amazon.S3;

namespace Utils.Aws.S3
{
    /// <summary>
    /// Support managing named <see cref="IAmazonS3"/> clients.
    /// </summary>
    /// <remarks>Note - as it stands this will only support Singletons</remarks>
    public class NamedAmazonS3ClientFactory : INamedAmazonS3ClientFactory
    {
        private readonly Dictionary<NamedClient, IAmazonS3> factory = new Dictionary<NamedClient, IAmazonS3>();
        
        public IAmazonS3 Get(NamedClient name) => factory[name];

        public void Add(NamedClient name, IAmazonS3 client) => factory[name] = client;
    }
}