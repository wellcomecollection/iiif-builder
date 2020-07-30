using Microsoft.AspNetCore.Builder;

namespace Wellcome.Dds.Server.Infrastructure
{
    /// <summary>
    /// https://stackoverflow.com/a/57197067
    /// </summary>
    public class SessionPipeline
    {
        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseSession();
        }
    }
}
