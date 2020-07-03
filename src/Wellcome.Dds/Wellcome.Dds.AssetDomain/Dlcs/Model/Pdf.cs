using System;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{

    /// <summary>
    /// The DLCS-generated PDF. This class holds some information about it for reporting purposes.
    /// Such as whether it exists, its size, its URL, access conditions etc.
    /// 
    /// Any errors?
    /// </summary>
    public class Pdf : IPdf
    {
        [JsonProperty(Order = 31, PropertyName = "url")]
        public string Url { get; set; }

        [JsonProperty(Order = 41, PropertyName = "exists")]
        public bool Exists { get; set; }

        [JsonProperty(Order = 51, PropertyName = "inProcess")]
        public bool InProcess { get; set; }
    
        [JsonProperty(Order = 61, PropertyName = "created")]
        public DateTime? Created { get; set; }

        [JsonProperty(Order = 71, PropertyName = "roles")]
        public string[] Roles { get; set; }

        [JsonProperty(Order = 81, PropertyName = "pageCount")]
        public int PageCount { get; set; }

        [JsonProperty(Order = 91, PropertyName = "sizeBytes")]
        public long SizeBytes { get; set; }
    }
}
