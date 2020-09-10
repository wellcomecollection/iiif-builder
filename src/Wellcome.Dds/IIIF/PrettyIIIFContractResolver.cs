using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IIIF
{
    public class PrettyIIIFContractResolver : CamelCasePropertyNamesContractResolver
    {
        // adapted from https://stackoverflow.com/a/34903827
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var pType = property.PropertyType;
            if (pType != null)
            {
                if (member.Name == "Width" || member.Name == "Height")
                {
                    property.ShouldSerialize = instance =>
                    {
                        var o = instance
                            .GetType()
                            .GetProperty(member.Name)
                            ?.GetValue(instance);
                        return o != null && (int) o != 0;
                    };
                }
                if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    property.ShouldSerialize = instance =>
                    {
                        IList? list = null;
                        if (member.MemberType == MemberTypes.Property)
                        {
                            list = instance
                                .GetType()
                                .GetProperty(member.Name)
                                ?.GetValue(instance, null) as IList;
                        }
                        return list != null && list.GetEnumerator().MoveNext();
                    };
                }
            }
            return property;
        }
    }
}