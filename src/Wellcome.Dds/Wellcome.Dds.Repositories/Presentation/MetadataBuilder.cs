using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3.Strings;
using Utils;
using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class MetadataBuilder
    {
        private readonly List<LabelValuePair> md = new();
        
        public MetadataBuilder(Work work)
        {
            // Use AddNoLang where the value of the metadata is:
            //    - possibly not in English
            //    - quantitative, or a date, or similar.
            // The labels will always be given an English language property, 
            // because they always are.
            
            AddEnglish("Description", work.Description);
            AddNonlang("Reference", work.ReferenceNumber);
            AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
            AddEnglish("Physical description", work.PhysicalDescription);
            AddNonlang("Contributors", work.Contributors.Select(c => c.Agent.Label));
            AddEnglish("Copyright note", work.GetNotes("copyright-note"));
            AddEnglish("Notes", work.GetNotes("general-note"));
            AddEnglish("Creator/production credits", work.GetNotes("credits"));
            AddEnglish("Type/technique", work.Genres.Select(g => g.Label));
            AddEnglish("Language", work.Language?.Label);
            AddEnglish("Subjects", work.Subjects.Select(s => s.Label));
            AddNonlang("Lettering", work.Lettering);
            AddEnglish("Publications note", work.GetNotes("publication-note"));
            AddEnglish("Reference", work.GetNotes("reference"));
            AddEnglish("Acquisition note", work.GetNotes("acquisition-note"));
        }

        private void AddNonlang(string label, IEnumerable<string> values)
        {
            Add(label, values.ToList(), "none");
        }
        
        private void AddEnglish(string label, IEnumerable<string> values)
        {
            Add(label, values.ToList(), "en");
        }

        private void AddEnglish(string label, string value)
        {
            Add(label, value, "en");
        }
        
        private void AddNonlang(string label, string value)
        {
            Add(label, value, "none");
        }

        private void Add(string label, string value, string valueLanguage)
        {
            if (value.HasText())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, value);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }
        
        
        private void Add(string label, IList<string> values, string valueLanguage)
        {
            if (values.Any())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, values);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }

        public List<LabelValuePair> Metadata => md;
    }
}