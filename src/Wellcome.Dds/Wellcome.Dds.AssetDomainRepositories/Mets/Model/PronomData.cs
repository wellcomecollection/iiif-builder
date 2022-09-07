using System;
using System.Collections.Generic;
using System.IO;
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
        var dataPath = Path.Combine(Environment.CurrentDirectory, "Data", "pronom_map.json");
        var formats = File.ReadAllText(dataPath);
        var rawDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(formats);
        // we only want the first mimetype if there are many
        formatMap = new Dictionary<string, string>(rawDict.Count);
        foreach (var kvp in rawDict)
        {
            if (kvp.Value.HasText())
            {
                formatMap[kvp.Key] = kvp.Value.Split(',')[0].Trim();
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