using Microsoft.AspNetCore.Builder;

namespace Wellcome.Dds.Server.Infrastructure
{
    /// <summary>
    /// A collection of extension methods for <see cref="IApplicationBuilder"/>
    /// </summary>
    public static class ApplicationBuilderX
    {
        /// <summary>
        /// Setup Swagger and UI for application.
        /// </summary>
        public static IApplicationBuilder SetupSwagger(this IApplicationBuilder app)
            => app
                .UseSwagger()
                .UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DDS Server V1");
                });

    }
}