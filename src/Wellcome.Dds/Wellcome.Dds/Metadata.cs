using System;
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
        public string Identifier { get; set; } // e.g., w7xpmx8r, or <StringValue>

        public Metadata(string manifestationId, string label, string stringValue, string identifier)
        {
            ManifestationId = manifestationId;
            Label = label;
            StringValue = stringValue;
            Identifier = identifier.HasText() ? identifier : UrlFriendlyValue(stringValue);
        }

        private string UrlFriendlyValue(string stringValue)
        {
            if (stringValue.Contains('_'))
            {
                // We only want to throw this during the big initial population, to see if this ever actually happens.
                throw new Exception("Metadata identifier contains underscore");
            }
            // See if this is enough. If there is only ONE replacement, it's more easily reversed.
            return stringValue.Replace(" ", "_");
        }

        public static string ToUrlFriendlyAggregator(string apiType)
        {
            // subjects, genres, contributor all work simply for now
            return apiType.ToLowerInvariant() + "s";
        }
        
        public static string FromUrlFriendlyAggregator(string pathElement)
        {
            // subjects, genres, contributor all work simply for now
            var initial = pathElement[0].ToString();
            return pathElement.Chomp("s").ReplaceFirst(initial, initial.ToUpperInvariant());
        }
    }
}