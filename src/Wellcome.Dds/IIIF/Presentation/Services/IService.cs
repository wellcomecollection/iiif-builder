﻿
namespace IIIF.Presentation.Services
{
    /// <summary>
    /// 
    /// </summary>
    public interface IService
    {
        string Id { get; set; }
        string Type { get; set; }
        
        // TODO string profile { get; set; } - on ResourceBase?
    }
}
