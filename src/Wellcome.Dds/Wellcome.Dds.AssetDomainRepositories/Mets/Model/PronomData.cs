using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Utils;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

public sealed class PronomData
{
    private static readonly PronomData instance = new PronomData();
    
    static PronomData()
    {
    }

    private PronomData()
    {                
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "pronom_map.json");
        var formats = File.ReadAllText(dataPath);
        var rawDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(formats);
        // we only want the first mimetype if there are many
        formatMap = new Dictionary<string, string>(rawDict.Count);
        foreach (var kvp in rawDict)
        {
            if (kvp.Value.HasText())
            {
                var mimes = kvp.Value.Split(',').Select(m => m.Trim()).ToArray();
                string mimeType = null;
                if (mimes.Length > 1)
                {
                    // if there are multiple mime types, favour those that are explicitly "paintable"
                    mimeType = mimes.FirstOrDefault(m => m.StartsWith("image/"));
                    if (mimeType.IsNullOrWhiteSpace())
                    {
                        mimeType = mimes.FirstOrDefault(m => m.StartsWith("video/"));
                    }
                    if (mimeType.IsNullOrWhiteSpace())
                    {
                        mimeType = mimes.FirstOrDefault(m => m.StartsWith("audio/"));
                    }
                    if (mimeType.IsNullOrWhiteSpace())
                    {
                        mimeType = mimes.FirstOrDefault(m => m.StartsWith("text/"));
                    }
                }
                if (mimeType.IsNullOrWhiteSpace())
                {
                    mimeType = mimes[0];
                }
                formatMap[kvp.Key] = mimeType?.Trim();
            }
            else
            {
                formatMap[kvp.Key] = null; // we could put a different mimetype here.
            }
        }
    }
    
    public static PronomData Instance => instance;

    private Dictionary<string, string> formatMap;


    public Dictionary<string, string> FormatMap => formatMap;
}