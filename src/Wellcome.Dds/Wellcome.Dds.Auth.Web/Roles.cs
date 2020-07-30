using System;

namespace Wellcome.Dds.Auth.Web
{
    public class Roles
    {
        private string[] roles;

        public Roles(string[] roles)
        {
            this.roles = roles;
        }

        public Roles(string serialisedRoles)
        {
            roles = serialisedRoles.Substring(2).Split('|', StringSplitOptions.RemoveEmptyEntries);
        }

        public override string ToString()
        {
            return "r-" + string.Join('|', roles);
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
                return Array.IndexOf(roles, "HHHHHHHHHHH TODO !!!!") != -1;
            }
        }
        public bool IsWellcomeStaffMember
        {
            get
            {
                return Array.IndexOf(roles, "WWWWWWWWW TODO !!!!") != -1;
            }
        }

        public DateTime Expires { get; set; }
    }
}
