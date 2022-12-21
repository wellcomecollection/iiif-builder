using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3.Strings;
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
            
            md.AddEnglish("Description", work.Description);
            md.AddNonlang("Reference", work.ReferenceNumber);
            md.AddNonlang("Publication/creation", work.Production?.FirstOrDefault()?.Label);
            md.AddEnglish("Physical description", work.PhysicalDescription);
            md.AddNonlang("Contributors", work.Contributors?.Select(c => c.Agent?.Label));
            md.AddEnglish("Copyright note", work.GetNotes("copyright-note"));
            md.AddEnglish("Notes", work.GetNotes("general-note"));
            md.AddEnglish("Creator/production credits", work.GetNotes("credits"));
            md.AddEnglish("Type/technique", work.Genres?.Select(g => g.Label));
            md.AddEnglish("Language", work.Language?.Label);
            md.AddEnglish("Subjects", work.Subjects?.Select(s => s.Label));
            md.AddNonlang("Lettering", work.Lettering);
            md.AddEnglish("Publications note", work.GetNotes("publication-note"));
            md.AddEnglish("Reference", work.GetNotes("reference"));
            md.AddEnglish("Acquisition note", work.GetNotes("acquisition-note"));
        }

        public List<LabelValuePair> Metadata => md;
    }
}