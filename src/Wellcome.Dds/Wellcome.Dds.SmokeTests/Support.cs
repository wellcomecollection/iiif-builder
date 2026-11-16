using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wellcome.Dds.SmokeTests;

public record Fetched(HttpStatusCode Status, string? ContentType, byte[] Body, Uri? FinalUri, Uri? Location = null)
{
    public string Text => System.Text.Encoding.UTF8.GetString(Body);
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
    public override string ToString() => $"{(int)Status} {ContentType} ({Body.Length} bytes) from {FinalUri}";
}

public static class Fetch
{
    /// <summary>GET a URL (absolute, or a path on the environment's host), following redirects.</summary>
    public static Task<Fetched> Get(string urlOrPath) => Send(SmokeEnvironment.Http, urlOrPath);

    /// <summary>GET without following a redirect; the target is in Location.</summary>
    public static Task<Fetched> GetWithoutRedirect(string urlOrPath) => Send(SmokeEnvironment.HttpNoRedirect, urlOrPath);

    private static async Task<Fetched> Send(HttpClient client, string urlOrPath)
    {
        var url = urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? urlOrPath
            : SmokeEnvironment.Url(urlOrPath);
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsByteArrayAsync();
        return new Fetched(response.StatusCode, response.Content.Headers.ContentType?.MediaType, body,
            response.RequestMessage?.RequestUri, response.Headers.Location);
    }
}

/// <summary>Image dimensions from the file header, without an imaging library.</summary>
public static class ImageHeader
{
    public static (int Width, int Height)? Png(byte[] b)
    {
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (b.Length < 24 || !b.Take(8).SequenceEqual(signature)) return null;
        return (ReadInt32BigEndian(b, 16), ReadInt32BigEndian(b, 20));
    }

    public static (int Width, int Height)? Jpeg(byte[] b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) return null;
            var marker = b[i + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                return (b[i + 7] << 8 | b[i + 8], b[i + 5] << 8 | b[i + 6]);
            }
            i += 2 + (b[i + 2] << 8 | b[i + 3]);
        }
        return null;
    }

    private static int ReadInt32BigEndian(byte[] b, int offset) =>
        b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3];
}

/// <summary>Reading the parts of IIIF resources the checks need, in either API version.</summary>
public static class Iiif
{
    public static string? Type(JsonElement resource) =>
        resource.TryGetProperty("type", out var t) ? t.GetString()
        : resource.TryGetProperty("@type", out var t2) ? t2.GetString()
        : null;

    public static string? Id(JsonElement resource) =>
        resource.TryGetProperty("id", out var i) ? i.GetString()
        : resource.TryGetProperty("@id", out var i2) ? i2.GetString()
        : null;

    /// <summary>A Presentation 3 Manifest's canvases: first, middle and last.</summary>
    public static List<JsonElement> SampleCanvases(JsonElement manifest)
    {
        var canvases = manifest.GetProperty("items").EnumerateArray().ToList();
        return new[] { 0, canvases.Count / 2, canvases.Count - 1 }
            .Distinct().Where(i => i >= 0 && i < canvases.Count).Select(i => canvases[i]).ToList();
    }

    /// <summary>The painting annotation's body on a Presentation 3 canvas (a Choice for some AV).</summary>
    public static JsonElement Body(JsonElement canvas) =>
        canvas.GetProperty("items")[0].GetProperty("items")[0].GetProperty("body");

    /// <summary>The image service id of a canvas's image body, if it has one.</summary>
    public static string? ImageServiceId(JsonElement canvas)
    {
        var body = Body(canvas);
        if (!body.TryGetProperty("service", out var services)) return null;
        return services.EnumerateArray().Select(Id).FirstOrDefault(id => id != null);
    }

    /// <summary>maxWidth from an info.json: top level (Image API 3) or in the profile object (Image API 2).</summary>
    public static int? MaxWidth(JsonElement info)
    {
        if (info.TryGetProperty("maxWidth", out var mw)) return mw.GetInt32();
        if (info.TryGetProperty("profile", out var profile) && profile.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in profile.EnumerateArray())
            {
                if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("maxWidth", out var pmw))
                {
                    return pmw.GetInt32();
                }
            }
        }
        return null;
    }

    /// <summary>The types (or profiles) of the services in an info.json, e.g. AuthProbeService2.</summary>
    public static List<string> ServiceTypes(JsonElement info)
    {
        if (!info.TryGetProperty("service", out var services) || services.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return services.EnumerateArray()
            .Select(s => Type(s) ?? (s.TryGetProperty("profile", out var p) ? p.ToString() : null))
            .Where(t => t != null).Select(t => t!).ToList();
    }

    /// <summary>The first thumbnail image of a Presentation 3 resource.</summary>
    public static string? ThumbnailId(JsonElement resource) =>
        resource.TryGetProperty("thumbnail", out var t) && t.GetArrayLength() > 0 ? Id(t[0]) : null;

    /// <summary>A service of an info.json or resource by type, e.g. AuthProbeService2, searching nested services.</summary>
    public static JsonElement? FindService(JsonElement resource, string type)
    {
        if (!resource.TryGetProperty("service", out var services)) return null;
        foreach (var s in services.ValueKind == JsonValueKind.Array ? services.EnumerateArray().ToList() : [services])
        {
            if (Type(s) == type) return s;
            if (FindService(s, type) is { } nested) return nested;
        }
        return null;
    }

    /// <summary>A 512px tile from the top-left of the image: a request only deep zoom makes.</summary>
    public static string TileUrl(string imageServiceId) => $"{imageServiceId}/0,0,512,512/512,/0/default.jpg";
}
