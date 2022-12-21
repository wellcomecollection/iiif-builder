using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Wellcome.Dds.Common
{
    public static class AccessCondition
    {
        // Access conditions that are known and expected in METS
        public const string Open = "Open";
        public const string RequiresRegistration = "Requires registration";
        public const string OpenWithAdvisory = "Open with advisory";
        public const string ClinicalImages = "Clinical images";
        public const string RestrictedFiles = "Restricted files";
        public const string Closed = "Closed";
        
        // Pseudo access conditions
        public const string Unknown = "Unknown"; // An access condition is present but it's not one of the above
        public const string Missing = "Missing"; // The METS contained no PREMIS rights statement, or MODS access condition


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
                new(Unknown, 5), 
                new(Missing, 5), 
                new(Closed, 5)
            };

        /// <summary>
        /// Is this access condition known to the DDS?
        /// </summary>
        /// <param name="s">The access condition in string form</param>
        /// <returns></returns>
        public static bool IsValid(string s)
        {
            return s is 
                Open or 
                RequiresRegistration or 
                OpenWithAdvisory or 
                ClinicalImages or 
                RestrictedFiles or 
                Closed or 
                Degraded;
        }

        /// <summary>
        /// Should the asset be included in a generated IIIF Manifest?
        /// NB an asset may be included in a Manifest even if it's not deliverable (not synced with DLCS)
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsForIIIFManifest(string s)
        {
            if (!IsValid(s))
            {
                return false;
            }

            if (s == Closed)
            {
                return false;
            }

            return true;
        }


        public static string GetMostSecureAccessCondition(IEnumerable<string> accessConditions)
        {
            return accessConditions.Select(s => SecurityOrder.Single(ac => ac.ToString() == s)).Max()!.ToString();
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

            public int CompareTo(ComparableAccessCondition? other)
            {
                if (other != null) return order.CompareTo(other.order);
                return 1;
            }
        }
    }
}
