using Amazon.S3;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    /// <summary>
    /// A marker interface so that we can distinguish between multiple IAmazonS3 impls available in a DI Container
    /// </summary>
    public interface IAmazonS3ForWellcomeStorageService : IAmazonS3
    {
    }
}
