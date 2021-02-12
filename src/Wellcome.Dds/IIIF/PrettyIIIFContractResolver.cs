using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IIIF
{
    /// <summary>
    /// TODO:
    ///
    /// (some of these need additional serialisers)
    ///
    ///  - format string arrays on a single line IF they have only one string and it is less than n chars
    ///  - format Size on one line: { "width": 100, "height": 200 }
    ///  - format label and value on one line, if they have a single string array that also formats to one line
    ///          "label": { "en": [ "Explore our collections" ] },
    /// 
    /// </summary>
    public class PrettyIIIFContractResolver : CamelCasePropertyNamesContractResolver
    {
        // adapted from https://stackoverflow.com/a/34903827
        
        // TODO: Serialise single val string arrays on one line
        // TODO: Serialise Sizes on one line
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var pType = property.PropertyType;
            if (pType != null)
            {
                // Don't serialise Width or Height if they have a zero value
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
                // Don't serialise empty lists, unless they have the [RequiredOutput] attribute
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
                        var hasContent = (list != null && list.GetEnumerator().MoveNext());
                        var requiredOutputAttr = member.GetCustomAttributes()
                            .FirstOrDefault(o => o is RequiredOutputAttribute);
                        return hasContent || requiredOutputAttr != null;
                    };
                }
            }
            return property;
        }
    }
}