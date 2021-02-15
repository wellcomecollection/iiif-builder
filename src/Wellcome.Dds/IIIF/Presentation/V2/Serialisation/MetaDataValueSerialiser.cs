using System;
using IIIF.Presentation.V2.Strings;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2.Serialisation
{
    /// <summary>
    /// JsonConverter for <see cref="MetaDataValue"/> objects.
    /// </summary>
    public class MetaDataValueSerialiser : WriteOnlyConverter
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var metaDataValue = value as MetaDataValue;
            if (metaDataValue == null)
            {
                throw new ArgumentException(
                    $"MetaDataValueSerialiser cannot serialise a {value.GetType().Name}", nameof(value));
            }

            if (metaDataValue.LanguageValues.Count == 0)
            {
                throw new ArgumentException(
                    $"MetaDataValueSerialiser cannot serialise an empty array {value.GetType().Name}", nameof(value));
            }

            if (metaDataValue.LanguageValues.Count > 1)
            {
                writer.WriteStartArray();
            }

            foreach (var lv in metaDataValue.LanguageValues)
            {
                if (string.IsNullOrWhiteSpace(lv.Language))
                {
                    writer.WriteValue(lv.Value);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("@value");
                    writer.WriteValue(lv.Value);
                    writer.WritePropertyName("@language");
                    writer.WriteValue(lv.Language);
                    writer.WriteEndObject();
                }
            }

            if (metaDataValue.LanguageValues.Count > 1)
            {
                writer.WriteEndArray();
            }
        }
    }
}