using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.Strings;
using Utils;
using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class MetadataBuilder
    {
        private readonly List<LabelValuePair> md = new List<LabelValuePair>();
        
        public MetadataBuilder(Work work)
        {
            // Use AddNoLang where the value of the metadata is:
            //    - possibly not in English
            //    - quantitative, or a date, or similar.
            // The labels will always be given an English language property, 
            // because they always are.
            
            /*
             New unified metadata - all work types have the same potential content
             They only get it if there are values
             */
            AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
            AddEnglish("Physical description", work.PhysicalDescription);
            AddNonlang("Contributors", work.Contributors.Select(c => c.Agent.Label));
            AddEnglish("Type/technique", work.Genres.Select(g => g.Label));
            AddEnglish("Description", work.Description);
            AddEnglish("Copyright note", work.GetNotes("copyright-note"));
            AddEnglish("Notes", work.GetNotes("general-note"));
            AddEnglish("Creator/production credits", work.GetNotes("credits"));
            AddNonlang("Lettering", work.Lettering);
            AddEnglish("Language", work.Language?.Label);
            AddEnglish("Publications note", work.GetNotes("publication-note"));
            AddNonlang("Reference", work.ReferenceNumber);
            AddEnglish("Reference notes", work.GetNotes("reference"));
            AddEnglish("Acquisition note", work.GetNotes("acquisition-note"));
            AddEnglish("Subjects", work.Subjects.Select(s => s.Label));
            /*
             * Old model - different work types got different metadata
             */
            
            /*
            switch (work.WorkType.Id)
            {
                case "a": // monograph
                    AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
                    AddEnglish("Physical description", work.PhysicalDescription);
                    AddNonlang("Contributors", work.Contributors.Select(c => c.Agent.Label));
                    AddEnglish("Type/technique", work.Genres.Select(g => g.Label));
                    AddEnglish("Language", work.Language?.Label);
                    AddEnglish("Subjects", work.Subjects.Select(s => s.Label));
                    break;
                case "i": // audio
                case "g": // video
                    AddEnglish("Description", work.Description);
                    AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
                    AddEnglish("Physical description", work.PhysicalDescription);
                    AddEnglish("Copyright note", work.GetNotes("copyright-note"));
                    AddEnglish("Notes", work.GetNotes("general-note"));
                    AddEnglish("Creator/production credits", work.GetNotes("credits"));
                    AddEnglish("Type/technique", work.Genres.Select(g => g.Label));
                    AddEnglish("Language", work.Language?.Label);
                    AddEnglish("Subjects", work.Subjects.Select(s => s.Label));
                    break;
                case "k": // artwork
                    AddEnglish("Description", work.Description);
                    AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
                    AddEnglish("Physical description", work.PhysicalDescription);
                    AddNonlang("Contributors", work.Contributors.Select(c => c.Agent.Label));
                    AddNonlang("Lettering", work.Lettering);
                    AddEnglish("Publications note", work.GetNotes("publication-note"));
                    AddEnglish("Reference", work.GetNotes("reference"));
                    AddEnglish("Type/technique", work.Genres.Select(g => g.Label));
                    break;
                case "h": // archive
                    AddEnglish("Description", work.Description);
                    AddNonlang("Reference", work.ReferenceNumber);
                    AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
                    AddEnglish("Physical description", work.PhysicalDescription);
                    AddEnglish("Acquisition note", work.GetNotes("acquisition-note"));
                    break;
                default: // same as archive, for now
                    AddEnglish("Description", work.Description);
                    AddNonlang("Reference", work.ReferenceNumber);
                    AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
                    AddEnglish("Physical description", work.PhysicalDescription);
                    AddEnglish("Acquisition note", work.GetNotes("acquisition-note"));
                    break;
            }
             */
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