using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Common
{
    public static class AccessCondition
    {
        public const string Open = "Open";
        public const string RequiresRegistration = "Requires registration";
        public const string OpenWithAdvisory = "Open with advisory";
        public const string ClinicalImages = "Clinical images";
        public const string Restricted = "Restricted"; // TODO - temporary addition, will be replaced by "Restricted files" in Archivematica METS"
        public const string RestrictedFiles = "Restricted files";
        public const string Closed = "Closed";

        public const string ClosedSectionError = "I will not serve a b number that has a closed section";

        // Added for IIIF experiment
        public const string Degraded = "Degraded";

        private static readonly List<ComparableAccessCondition> SecurityOrder = new()
            {
                new(Open, 0),
                new(Degraded, 1),
                new(RequiresRegistration, 2),
                new(OpenWithAdvisory, 2),
                new(ClinicalImages, 3),
                new(RestrictedFiles, 4),
                new(Restricted, 4), // TODO: temporary, see above
                new(Closed, 5)
            };

        public static bool IsValid(string s)
        {
            return (s == Open || 
                    s == RequiresRegistration || 
                    s == OpenWithAdvisory ||
                    s == ClinicalImages || 
                    s == RestrictedFiles || 
                    s == Restricted ||  // TODO: temporary, see above
                    s == Closed || 
                    s == Degraded);
        }


        public static string GetMostSecureAccessCondition(IEnumerable<string> accessConditions)
        {
            return accessConditions.Select(s => SecurityOrder.Single(ac => ac.ToString() == s)).Max().ToString();
        }


        private class ComparableAccessCondition : IComparable<ComparableAccessCondition>
        {
            private readonly string ac;
            private readonly int order;

            public ComparableAccessCondition(string ac, int order)
            {
                this.ac = ac;
                this.order = order;
            }

            public override string ToString()
            {
                return ac;
            }

            public int CompareTo(ComparableAccessCondition other)
            {
                return order.CompareTo(other.order);
            }
        }
    }
}
