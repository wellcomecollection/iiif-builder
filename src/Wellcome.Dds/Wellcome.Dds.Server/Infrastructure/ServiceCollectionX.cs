using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Wellcome.Dds.Server.Infrastructure
{
    /// <summary>
    /// A collection of extension methods for <see cref="IServiceCollection"/>
    /// </summary>
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add swagger definition.
        /// </summary>
        public static IServiceCollection AddSwagger(this IServiceCollection services)
            => services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "DDS Server", Version = "v1"});

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
    }
}