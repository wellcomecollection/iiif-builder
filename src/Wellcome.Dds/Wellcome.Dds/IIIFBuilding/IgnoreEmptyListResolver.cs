using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Wellcome.Dds.IIIFBuilding
{
    public class IgnoreEmptyListResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var pType = property.PropertyType;
            if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(List<>))
            {
                property.ShouldSerialize = instance =>
                {
                    IList list = null;
                    // this value could be in a public field or public property
                    switch (member.MemberType)
                    {
                        case MemberTypes.Property:
                            list = instance
                                .GetType()
                                .GetProperty(member.Name)
                                ?.GetValue(instance, null) as IList;
                            break;
                        case MemberTypes.Field:
                            list = instance
                                .GetType()
                                .GetField(member.Name)
                                .GetValue(instance) as IList;
                            break;
                    }

                    return list == null || list.GetEnumerator().MoveNext();
                    // if the list is null, we defer the decision to NullValueHandling
                };
            }

            return property;
        }
    }
}