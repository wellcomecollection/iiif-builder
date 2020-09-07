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

        private readonly string[] sierraRoles;

        public Roles(string[] sierraRoles, DateTime expires, string[] barcodes)
        {
            this.sierraRoles = sierraRoles;
            Expires = expires;
            BarCodes = barcodes;
        }

        public Roles(string serialisedRoles)
        {
            var parts = serialisedRoles.Substring(2)
                .Split('|', StringSplitOptions.RemoveEmptyEntries).ToArray();
            sierraRoles = parts.SkipLast(2).ToArray();
            Expires = DateTime.Parse(parts[^2].Substring(2));
            BarCodes = parts[^1].Substring(2)
                .Split(',', StringSplitOptions.RemoveEmptyEntries).ToArray();
        }

        public override string ToString()
        {
            var roles = string.Join('|', sierraRoles);
            var expires = Expires.ToString("yyyy-MM-dd");
            var barcodes = string.Join(',', BarCodes);
            return $"r-{roles}|e-{expires}|b-{barcodes}";
        }

        // This will always be true in new DDS 
        // (ability to use clickthrough) - defer that to wc.org site, no way of getting it here.
        public bool HasAcceptedTerms => true;

        public bool IsHealthCareProfessional 
            => sierraRoles.Contains($"{HealthCareProfessionalFieldTag}:True");
        
        public bool IsWellcomeStaffMember 
            => sierraRoles.Contains($"{PseudoWellcomeStaffTag}:True");

        // this is currently not used, but could be
        public bool HasCompletedRestrictedAccessForm 
            => sierraRoles.Contains($"{RestrictedArchiveFieldTag}:True");

        public DateTime Expires { get; set; }
        
        public string[] BarCodes { get; set; }

        public string[] GetSierraRoles() => sierraRoles;

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
