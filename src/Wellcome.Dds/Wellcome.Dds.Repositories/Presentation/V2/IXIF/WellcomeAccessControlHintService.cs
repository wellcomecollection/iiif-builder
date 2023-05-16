﻿using System.Collections.Generic;
using IIIF;
using IIIF.Presentation.V2;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// This is a service that is used by wl.org UV for rendering iiif p2.
    /// This is not valid IIIF service but is required for backward compatibility.
    /// </summary>
    public class WellcomeAccessControlHintService : ResourceBase, IService
    {
        public override string? Type { get; set; }

        [JsonProperty(Order = 14, PropertyName = "accessHint")]
        public string? AccessHint { get; set; }

        [JsonProperty(Order = 15, PropertyName = "authService")]
        [ObjectIfSingle]
        public List<IService>? AuthService { get; set; }
    }
}