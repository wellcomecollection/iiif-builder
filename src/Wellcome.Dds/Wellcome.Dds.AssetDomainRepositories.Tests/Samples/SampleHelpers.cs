using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Samples
{
    public class SampleHelpers
    {
        public static JObject GetJson(string fileName)
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);
            var json = File.ReadAllText(filePath);
            return JObject.Parse(json);
        }
    }
}