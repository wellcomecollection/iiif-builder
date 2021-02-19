using IIIF.Presentation.V2.Serialisation;
using Newtonsoft.Json;

namespace IIIF.Serialisation
{
    /// <summary>
    /// Converter for <see cref="ServiceReference"/>, will output type and id if type present, else just id as string
    /// </summary>
    public class ServiceReferenceConverter : WriteOnlyConverter<ServiceReference>
    {
        public override void WriteJson(JsonWriter writer, ServiceReference? value, JsonSerializer serializer)
        {
            if (string.IsNullOrEmpty(value?.Type) && !string.IsNullOrEmpty(value?.Id))
            {
                writer.WriteValue(value!.Id);
            }
            else
            {
                writer.WriteRawValue(JsonConvert.SerializeObject(value));
            }
        }
    }
}