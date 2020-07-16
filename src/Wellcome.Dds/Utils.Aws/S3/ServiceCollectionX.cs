using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Utils.Aws.S3
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Adds <see cref="INamedAmazonS3ClientFactory"/> as singleton to <see cref="IServiceCollection"/> with specified clients.
        /// Appropriate appsetting values must be available (Dds-AWS and/or Storage-AWS)
        /// </summary>
        /// <param name="services">Current <see cref="IServiceCollection"/> object.</param>
        /// <param name="configuration">Current <see cref="IConfiguration"/> object.</param>
        /// <param name="clients">Named clients to add.</param>
        /// <returns>ClientFactory registered in IoC</returns>
        public static INamedAmazonS3ClientFactory AddNamedS3Clients(this IServiceCollection services, IConfiguration configuration,
            NamedClient clients)
        {
            var factory = new NamedAmazonS3ClientFactory();
            
            if (clients.HasFlag(NamedClient.Dds))
            {
                var awsOptions = configuration.GetAWSOptions("Dds-AWS");
                factory.Add(NamedClient.Dds, awsOptions.CreateServiceClient<IAmazonS3>());
            }
            
            if (clients.HasFlag(NamedClient.Storage))
            {
                var awsOptions = configuration.GetAWSOptions("Storage-AWS");
                factory.Add(NamedClient.Storage, awsOptions.CreateServiceClient<IAmazonS3>());
            }
            
            services.AddSingleton<INamedAmazonS3ClientFactory>(factory);

            return factory;
        }
    }
}