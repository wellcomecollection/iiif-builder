using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Utils;

namespace Wellcome.Dds
{
    public class Metadata
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }
        public string ManifestationId { get; set; }
        public string Label { get; set; } // e.g., Subject, Genre, Contributor
        public string StringValue { get; set; } // e.g., 
        public string Identifier { get; set; } // e.g., w7xpmx8r, or 

        public Metadata(string manifestationId, string label, string stringValue, string identifier)
        {
            ManifestationId = manifestationId;
            Label = label;
            StringValue = stringValue;
            Identifier = identifier.HasText() ? identifier : stringValue;
        }
    }
    
}