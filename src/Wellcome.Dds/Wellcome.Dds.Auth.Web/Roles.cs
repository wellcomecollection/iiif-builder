using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Auth.Web
{
    public class Roles
    {
        public const string ClosedArchiveFieldTag = "d";
        public const string HealthCareProfessionalFieldTag = "k";
        public const string RestrictedArchiveFieldTag = "r";
        public const string PatronTypeFieldTag = "47";
        public const string PatronExpiryFieldTag = "43";
        public const string PseudoWellcomeStaffTag = "w";

        private string[] sierraRoles;

        public Roles(string[] sierraRoles, DateTime expires)
        {
            this.sierraRoles = sierraRoles;
            this.Expires = expires;
        }

        public Roles(string serialisedRoles)
        {
            var parts = serialisedRoles.Substring(2).Split('|', StringSplitOptions.RemoveEmptyEntries);
            sierraRoles = parts.SkipLast(1).ToArray();
            Expires = DateTime.Parse(parts.Last());
        }

        public override string ToString()
        {
            return "r-" + string.Join('|', sierraRoles) + "|" + Expires.ToString("yyyy-MM-dd");
        }

        public bool HasAcceptedTerms
        {
            // This will always be true in new DDS 
            // (ability to use clickthrough) - defer that to wc.org site, no way of getting it here.
            get { return true; }
        }

        public bool IsHealthCareProfessional
        {
            get
            {
                return Array.IndexOf(sierraRoles, $"{HealthCareProfessionalFieldTag}:True") != -1;
            }
        }
        public bool IsWellcomeStaffMember
        {
            get
            {
                return Array.IndexOf(sierraRoles, $"{PseudoWellcomeStaffTag}:True") != -1;
            }
        }

        // this is currently not used, but could be
        public bool HasCompletedRestrictedAccessForm
        {
            get
            {
                return Array.IndexOf(sierraRoles, $"{RestrictedArchiveFieldTag}:True") != -1;
            }
        }

        public DateTime Expires { get; set; }

        public string[] GetSierraRoles()
        {
            return sierraRoles;
        }

        public string[] GetDlcsRoles()
        {
            const string clickthrough = "https://api.dlcs.io/customers/2/roles/clickthrough";
            const string clinicalImages = "https://api.dlcs.io/customers/2/roles/clinicalImages";
            const string restrictedFiles = "https://api.dlcs.io/customers/2/roles/restrictedFiles";
            // const string closed = "https://api.dlcs.io/customers/2/roles/closed";

            var dlcsRoles = new HashSet<string>();
            if (HasAcceptedTerms)
            {
                dlcsRoles.Add(clickthrough);
            }
            if (IsHealthCareProfessional)
            {
                dlcsRoles.Add(clickthrough);
                dlcsRoles.Add(clinicalImages);
            }
            if (IsWellcomeStaffMember)
            {
                dlcsRoles.Add(clickthrough);
                dlcsRoles.Add(clinicalImages);
                dlcsRoles.Add(restrictedFiles);
            }
            if(HasCompletedRestrictedAccessForm)
            {
                // nothing, but there could be!
            }

            return dlcsRoles.ToArray();
        }
    }
}
