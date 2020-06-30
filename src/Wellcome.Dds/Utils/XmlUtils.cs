using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;

namespace Utils
{
    public static class XmlUtils
    {
        public static XElement GetSingleElementWithAttribute(this XElement xel, XName elementName, string attributeName, string attributeValue)
        {
            return xel
                .Elements(elementName)
                .Single(x => (string)x.Attribute(attributeName) == attributeValue);
        }

        public static XElement GetSingleDescendantWithAttribute(this XElement xel, XName elementName, string attributeName, string attributeValue)
        {
            return xel
                .Descendants(elementName)
                .Single(x => (string)x.Attribute(attributeName) == attributeValue);
        }

        public static IEnumerable<XElement> GetAllDescendantsWithAttribute(this XElement xel, XName elementName, string attributeName, string attributeValue)
        {
            return xel
                .Descendants(elementName)
                .Where(x => (string)x.Attribute(attributeName) == attributeValue);
        }

        public static string GetDesendantElementValue(this XDocument xel, XName elementName)
        {
            try
            {
                var el = xel.Descendants(elementName).FirstOrDefault();
                if (el != null)
                {
                    return el.Value;
                }
            }
            catch (Exception)
            {
                // TODO:Log
            }
            return null;
        }
        public static string GetDesendantElementValue(this XElement xel, XName elementName)
        {
            try
            {
                var el = xel.Descendants(elementName).FirstOrDefault();
                if (el != null)
                {
                    return el.Value;
                }
            }
            catch (Exception)
            {
                // TODO:Log
            }
            return null;
        }

        public static IEnumerable<string> GetDesendantElementValues(this XDocument xel, XName elementName)
        {
            var els = xel.Descendants(elementName);
            return els.Select(el => el.Value.Trim());
        }

        public static string GetRequiredAttributeValue(this XElement xel, XName attributeName)
        {
            if (xel == null)
            {
                throw new NoNullAllowedException("Missing required element, nothing to get " + attributeName + " from.");
            }
            var attr = xel.Attribute(attributeName);
            if (attr == null)
            {
                throw new NoNullAllowedException("Missing required attribute (" + attributeName + ") on element: " + xel);
            }
            return attr.Value;
        }

        public static string GetAttributeValue(this XElement xel, XName attributeName, string valueIfMissing)
        {
            if (xel == null)
            {
                throw new NoNullAllowedException("Missing required element, nothing to get " + attributeName + " from.");
            }
            var attr = xel.Attribute(attributeName);
            if (attr == null)
            {
                return valueIfMissing;
            }
            return attr.Value;
        }
    }
}
