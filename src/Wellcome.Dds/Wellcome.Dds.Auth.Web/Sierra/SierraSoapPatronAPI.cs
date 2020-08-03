using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Utils;
using Wellcome.Dds.Common;
using Wellcome.Dds.MillenniumClient;

namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class SierraSoapPatronAPI : IUserService
    {
        private DdsOptions ddsOptions;
        private ILogger<SierraSoapPatronAPI> logger;



        public SierraSoapPatronAPI(
            ILogger<SierraSoapPatronAPI> logger,
            IOptions<DdsOptions> options)
        {
            this.logger = logger;
            ddsOptions = options.Value;
        }

        public async Task<UserRolesResult> GetUserRoles(string username)
        {
            string millenniumVersion = "b" + username;
            return await GetUserInternal(millenniumVersion);
        }

        private async Task<UserRolesResult> GetUserInternal(string searchString)
        {
            var result = new UserRolesResult();
            var basicHttpBinding = new BasicHttpBinding();
            EndpointAddress endpointAddress = new EndpointAddress(ddsOptions.PatronApiEndpoint);
            var client = new PatronIOClient(basicHttpBinding, endpointAddress);
            try
            {
                var patron = await client.searchPatronsAsync(
                    ddsOptions.MillenniumUserName,
                    ddsOptions.MillenniumPassword,
                    searchString);
                var patronRoles = new List<string>();
                if (patron != null)
                {
                    foreach (var patronField in patron.patronFields)
                    {
                        switch (patronField.fieldTag)
                        {
                            case Roles.ClosedArchiveFieldTag:
                                // HasPermissionToViewClosedArchive
                                patronRoles.Add($"{Roles.ClosedArchiveFieldTag}:{ReadMillenniumBool(patronField.value)}");
                                break;

                            case Roles.HealthCareProfessionalFieldTag:
                                // IsHealthCareProfessional
                                patronRoles.Add($"{Roles.HealthCareProfessionalFieldTag}:{ReadMillenniumBool(patronField.value)}");
                                break;

                            case Roles.RestrictedArchiveFieldTag:
                                // HasCompletedRestrictedAccessForm
                                patronRoles.Add($"{Roles.RestrictedArchiveFieldTag}:{ReadMillenniumBool(patronField.value)}");
                                break;

                            case Roles.PatronTypeFieldTag:
                                // IsWellcomeStaffMember
                                patronRoles.Add($"{Roles.PseudoWellcomeStaffTag}:{PatronIsWellcomeStaff(patronField.value)}");
                                break;
                        }
                    }

                    var expires = GetExpiryDate(patron.patronFields.Single(f => f.fieldTag == "43").value);
                    var roles = new Roles(patronRoles.ToArray(), expires);

                    result.Roles = roles;
                    result.Success = true;
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, $"Millennium web service error: {ex.Message}");
                result.Message = ex.Message;
            }
            return result;
        }

        private bool PatronIsWellcomeStaff(string patronTypeFieldValue)
        {
            int fieldVal;
            if (int.TryParse(patronTypeFieldValue, out fieldVal))
            {
                // return (fieldVal == 0 || fieldVal == 1); // this is temporarily the test in development
                return fieldVal == 8; // This will be the test in production
            }
            return false;
        }


        private DateTime GetExpiryDate(string value)
        {
            DateTime dt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(value))
            {
                logger.LogInformation($"no expiry date in string {value}");
                return dt;
            }
            try
            {
                dt = new DateTime(
                    Convert.ToInt32(value.Substring(0, 4)),
                    Convert.ToInt32(value.Substring(4, 2)),
                    Convert.ToInt32(value.Substring(6, 2)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Unable to parse expiry date from {value}");
            }
            return dt;
        }

        /// <summary>
        /// Copied straight from old version 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool ReadMillenniumBool(string value)
        {
            // this should be really simple but there might be oddities. We'll assume false.
            bool b = StringUtils.GetBoolValue(value, false);

            // that should be it, but temporarily assume that Natalie's text in the field means "true"...
            // TODO: check with Natalie if this is safe to remove
            if (value.Contains("true/false"))
            {
                b = true;
            }
            return b;
        }
    }
}