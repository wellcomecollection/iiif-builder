using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wellcome.Dds.Auth.Web;

namespace Wellcome.Dds.Server.Models
{
    /// <summary>
    /// Not used in production, just for test and demo purposes
    /// </summary>
    public class RoleProviderInfoModel
    {
        public string SuppliedToken { get; internal set; }
        public Roles RolesFromToken { get; internal set; }
        public int SessionFlag { get; internal set; }
        public Roles RolesFromSession { get; internal set; }
    }
}
