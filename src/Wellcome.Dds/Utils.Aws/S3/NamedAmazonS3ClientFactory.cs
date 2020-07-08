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
        private readonly Dictionary<string, IAmazonS3> _factory = new Dictionary<string, IAmazonS3>();
        
        public IAmazonS3 Get(string name) => _factory[name];

        public void Add(string name, IAmazonS3 client) => _factory[name] = client;
    }
}