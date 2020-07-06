using Amazon.S3;

namespace Utils.Storage.S3
{
    /// <summary>
    /// Marker interface for identifying the correct IAmazonS3 impl to use for storage
    /// </summary>
    public interface IAmazonS3ForCacheStorage : IAmazonS3
    {
    }
}
