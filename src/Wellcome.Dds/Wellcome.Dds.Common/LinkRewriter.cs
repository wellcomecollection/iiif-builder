using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Utils;

namespace Wellcome.Dds.Common;

/// <summary>
/// Transforms any string containing links, to modify DDS and DLCS URLs
/// This allows for previewing and testing even when loading stored Manifests from S3 that contain "proper" URLs
///
/// We want to by default generate our IIIF with the cloudfront paths for both DDS and DLCS.
/// For DLCS this means without the space parameter in the URL pattern.
///
/// For testing we might want to modify the DDS paths to localhost and/or the DLCS paths to a different DLCS.
/// We need to be able to do these things independently.
///
/// This means that appSettings might contain one schema and host for generating iiif.wc.org links, that the
/// DDS uses for both DDS and DLCS, and then appSettings might also have further settings to modify these links
/// at the last minute.
/// </summary>
public class LinkRewriter
{
    private readonly DdsOptions ddsOptions;

    public LinkRewriter(IOptions<DdsOptions> options)
    {
        ddsOptions = options.Value;
        
        // need to handle -- api.wellcomecollection.org/text
    }

    public bool RequiresRewriting()
    {
        return ddsOptions.RewriteDomainLinksTo.HasText() || ddsOptions.RewriteDlcsLinksHostTo.HasText();
    }

    public async Task<string> RewriteLinks(StreamReader streamReader)
    {
        string raw = await streamReader.ReadToEndAsync();
        return RewriteLinks(raw);
    }

    public string RewriteLinks(string raw)
    {
        string result = raw;

        // we need to transform the DLCS links and the DDS links separately.
        // We need to remove the DLCS links from consideration
        var pattern = GetDlcsLinksRegexPattern(ddsOptions.LinkedDataDomain!);
        var placeholder = "__DLCS_DOMAIN_PLACEHOLDER__";
        result = Regex.Replace(raw, pattern, $"\"{placeholder}/$1/$2\"");
        
        // we have now separated the DDS and DLCS links so we can do replacements on them independently
        if (ddsOptions.RewriteDomainLinksTo.HasText())
        {
            var newLink = ddsOptions.RewriteDomainLinksTo;
            result = result.Replace(ddsOptions.LinkedDataDomain!, newLink);
            result = result.Replace("https://api.wellcomecollection.org/text/", $"{newLink}/text/");
        }

        if (ddsOptions.RewriteDlcsLinksSpaceTo is > 0)
        {
            // modify the space part of the DLCS URLs
            var space = ddsOptions.RewriteDlcsLinksSpaceTo;
            
            // do all of (image|thumbs|pdf|av|auth) use the same pattern? No.
            var imgPattern = GetDlcsLinksRegexPattern(placeholder, "image");
            result = Regex.Replace(result, imgPattern, $"\"{placeholder}/iiif-img/2/{space}/$2\"");

            var avPattern = GetDlcsLinksRegexPattern(placeholder, "av");
            result = Regex.Replace(result, avPattern, $"\"{placeholder}/iiif-av/2/{space}/$2\"");
            
            var pdfPattern = GetDlcsLinksRegexPattern(placeholder, "pdf");
            result = Regex.Replace(result, pdfPattern, $"\"{placeholder}/pdf/2/pdf/{space}/$2\"");
            
            var authPattern = GetDlcsLinksRegexPattern(placeholder, "auth");
            result = Regex.Replace(result, authPattern, $"\"{placeholder}/auth/2/$2\""); // no space
            
            // default (ok for thumbs and file)
            pattern = GetDlcsLinksRegexPattern(placeholder, "thumbs|file");
            result = Regex.Replace(result, pattern, $"\"{placeholder}/$1/2/{space}/$2\"");
            
        }
        
        // now either modify the scheme and host part of the DLCS URLs or return it to its original state
        var dlcsHost = ddsOptions.RewriteDlcsLinksHostTo.HasText()
            ? ddsOptions.RewriteDlcsLinksHostTo
            : ddsOptions.LinkedDataDomain;

        result = result.Replace(placeholder, dlcsHost);

        return result;
    }
    
    private string GetDlcsLinksRegexPattern(string domainPart, string? part = null)
    {
        var pattern = "\\\"" + domainPart.Replace(".", "\\.");
        var firstPathElement = part ?? "image|thumbs|pdf|av|auth|file";
        pattern += "/(" + firstPathElement + ")/([^\"]*)\\\"";
        return pattern;
    }

    public string TransformIdentifier(string identifier)
    {
        if (ddsOptions.RewriteDomainLinksTo.IsNullOrWhiteSpace())
        {
            return identifier;
        }

        return identifier.Replace(ddsOptions.LinkedDataDomain!, ddsOptions.RewriteDomainLinksTo);
    }
}