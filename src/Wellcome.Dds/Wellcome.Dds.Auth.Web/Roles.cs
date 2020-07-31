using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Auth.Web
{
    public class Roles
    {
        private string[] sierraRoles;

        public Roles(string[] sierraRoles)
        {
            this.sierraRoles = sierraRoles;
        }

        public Roles(string serialisedRoles)
        {
            sierraRoles = serialisedRoles.Substring(2).Split('|', StringSplitOptions.RemoveEmptyEntries);
        }

        public override string ToString()
        {
            return "r-" + string.Join('|', sierraRoles);
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
                return Array.IndexOf(sierraRoles, "HHHHHHHHHHH TODO !!!!") != -1;
            }
        }
        public bool IsWellcomeStaffMember
        {
            get
            {
                return Array.IndexOf(sierraRoles, "WWWWWWWWW TODO !!!!") != -1;
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

            return dlcsRoles.ToArray();
        }
    }
}
