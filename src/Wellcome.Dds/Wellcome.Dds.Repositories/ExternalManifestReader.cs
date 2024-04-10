using System.Net.Http;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wellcome.Dds.Repositories;

public class ExternalManifestReader
{
    private readonly ILogger<ExternalManifestReader> logger;
    private readonly HttpClient httpClient;
    private readonly DlcsOptions dlcsOptions;

    public ExternalManifestReader(
        ILogger<ExternalManifestReader> logger,
        HttpClient httpClient,
        IOptions<DlcsOptions> dlcsOptions)
    {
        this.dlcsOptions = dlcsOptions.Value;
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<Manifest> LoadDlcsManifest(string identifier)
    {
        var namedQueryManifestUri = string.Format(
            dlcsOptions.SkeletonNamedQueryTemplate!, dlcsOptions.CustomerDefaultSpace, identifier);

        var manifestStream = await httpClient.GetStreamAsync(namedQueryManifestUri);
        return manifestStream.FromJsonStream<Manifest>();
    }
    
}