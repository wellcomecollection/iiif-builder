using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class MetadataExtensions
    {
        public static void AddNonlang(this List<LabelValuePair> md, string label, IEnumerable<string?>? values)
        {
            if (values != null)
            {
                md.Add(label, values.OfType<string>().ToList(), "none");
            }
        }
        public static void AddNonLangMetadata(this ResourceBase resource, string label, IEnumerable<string> values)
        {
            resource.Metadata ??= new List<LabelValuePair>();
            resource.Metadata.AddNonlang(label, values);
        }
        
        public static void AddEnglish(this List<LabelValuePair> md, string label, IEnumerable<string?>? values)
        {
            if (values != null)
            {
                md.Add(label, values.OfType<string>().ToList(), "en");
            }
        }
        public static void AddEnglishMetadata(this ResourceBase resource, string label, IEnumerable<string> values)
        {
            resource.Metadata ??= new List<LabelValuePair>();
            resource.Metadata.AddEnglish(label, values);
        }
        
        public static void AddEnglishMetadata(this ResourceBase resource, string label, string value)
        {
            resource.Metadata ??= new List<LabelValuePair>();
            resource.Metadata.AddEnglish(label, value);
        }
        public static void AddEnglish(this List<LabelValuePair> md, string label, string? value)
        {
            md.Add(label, value, "en");
        }
        
        public static void AddNonLangMetadata(this ResourceBase resource, string label, string? value)
        {
            resource.Metadata ??= new List<LabelValuePair>();
            resource.Metadata.AddNonlang(label, value);
        }
        public static void AddNonlang(this List<LabelValuePair> md, string label, string? value)
        {
            md.Add(label, value, "none");
        }

        private static void Add(this List<LabelValuePair> md, string label, string? value, string valueLanguage)
        {
            if (value.HasText())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, value);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }

        public static void AddMetadataPair(this ResourceBase resource, LabelValuePair? pair)
        {
            if (pair != null)
            {
                resource.Metadata ??= new List<LabelValuePair>();
                resource.Metadata.Add(pair);
            }
        }


        private static void Add(this List<LabelValuePair> md, string label, IList<string> values, string valueLanguage)
        {
            if (values.Any())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, values);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }
        
    }
}