using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OAuth2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Utils;
using Wellcome.Dds.Auth.Web.Sierra.ApiModel;

namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class SierraRestPatronApi : IUserService, IAuthenticationService
    {
        private OAuth2ApiConsumer oAuth2ApiConsumer;
        private SierraRestApiOptions sierraRestAPIOptions;
        private ILogger<SierraRestPatronApi> logger;
        private ClientCredentials clientCredentials;
        private JsonSerializerSettings jsonSerializerSettings;

        public SierraRestPatronApi(
            ILogger<SierraRestPatronApi> logger,
            IOptions<SierraRestApiOptions> options,
            OAuth2ApiConsumer oAuth2ApiConsumer)
        {
            this.oAuth2ApiConsumer = oAuth2ApiConsumer;
            sierraRestAPIOptions = options.Value;
            this.logger = logger;
            clientCredentials = new ClientCredentials
            {
                TokenEndPoint = sierraRestAPIOptions.TokenEndPoint,
                Scope = sierraRestAPIOptions.Scope,
                ClientId = sierraRestAPIOptions.ClientId,
                ClientSecret = sierraRestAPIOptions.ClientSecret
            };
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = contractResolver };
        }

        public async Task<UserRolesResult> GetUserRoles(string nameCredential)
        {
            const string requiredFields = "varFields,expirationDate,patronType,barcodes";
            var userNameSearchUrl = $"{sierraRestAPIOptions.PatronFindUrl}?varFieldTag=s&varFieldContent={nameCredential}&fields={requiredFields}";
            var result = await GetUserRolesFromApiCall(userNameSearchUrl);
            if (!result.Success)
            {
                var barcodeQueryUrl = $"{sierraRestAPIOptions.PatronGetUrl}{nameCredential}?fields={requiredFields}";
                result = await GetUserRolesFromApiCall(barcodeQueryUrl);
            }
            return result;
        }

        private async Task<UserRolesResult> GetUserRolesFromApiCall(string url)
        {
            var result = new UserRolesResult();
            try
            {
                var patron = await oAuth2ApiConsumer.GetOAuthedJToken(url, clientCredentials, false);
                var varFields = patron["varFields"];
                if (varFields != null)
                {
                    var patronRoles = new List<string>();
                    foreach (var field in varFields)
                    {
                        switch (field.Value<string>("fieldTag"))
                        {
                            case Roles.ClosedArchiveFieldTag:
                                // HasPermissionToViewClosedArchive
                                patronRoles.Add(
                                    $"{Roles.ClosedArchiveFieldTag}:{ReadMillenniumBool(field.Value<string>("content"))}");
                                break;

                            case Roles.HealthCareProfessionalFieldTag:
                                // IsHealthCareProfessional
                                patronRoles.Add(
                                    $"{Roles.HealthCareProfessionalFieldTag}:{ReadMillenniumBool(field.Value<string>("content"))}");
                                break;

                            case Roles.RestrictedArchiveFieldTag:
                                // HasCompletedRestrictedAccessForm
                                patronRoles.Add(
                                    $"{Roles.RestrictedArchiveFieldTag}:{ReadMillenniumBool(field.Value<string>("content"))}");
                                break;

                            // The following tags don't come back in the REST API (unlike the SOAP API)

                            //case Roles.PatronTypeFieldTag:
                            //    // IsWellcomeStaffMember
                            //    patronRoles.Add($"{Roles.PseudoWellcomeStaffTag}:{PatronIsWellcomeStaff(field.Value<string>("content"))}");
                            //    break;

                            // This tag doesn't come back in varFields.
                            //case Roles.PatronExpiryFieldTag:
                            //    expiryDate = GetExpiryDate(field.Value<string>("content"));
                            //    break;
                        }
                    }

                    var expires = GetExpiryDate(patron.Value<string>("expirationDate"));
                    var barcodes = patron.Value<JArray>("barcodes").Values<string>().ToArray();
                    var patronType = patron.Value<string>("patronType");
                    patronRoles.Add($"{Roles.PseudoWellcomeStaffTag}:{PatronIsWellcomeStaff(patronType)}");
                    var roles = new Roles(patronRoles.ToArray(), expires, barcodes);

                    result.Roles = roles;
                    result.Success = true;
                }
                else if (patron["name"] != null)
                {
                    result.Message = patron.Value<string>("name");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Sierra REST API error: {ex.Message}");
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
            // TODO - could we use DateTime.TryParse or DateTime.Parse here?
            // The REST API returns the date in a different format...
            int monthStart = 4;
            int dayStart = 6;
            if(value.Contains("-"))
            {
                monthStart = 5;
                dayStart = 8;
            }
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
                    Convert.ToInt32(value.Substring(monthStart, 2)),
                    Convert.ToInt32(value.Substring(dayStart, 2)));
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


        public async Task<AuthenticationResult> Authenticate(string username, string password)
        {
            var patronValidation = new PatronValidation
            {
                Barcode = username,
                Pin = password,
                CaseSensitivity = false
            };

            var body = JsonConvert.SerializeObject(patronValidation, jsonSerializerSettings);
            var apiResult = await oAuth2ApiConsumer.PostBody(sierraRestAPIOptions.PatronValidateUrl, body, clientCredentials);
            var result = new AuthenticationResult
            {
                Success = apiResult.HttpStatusCode == System.Net.HttpStatusCode.NoContent
            };
            if (!result.Success)
            {
                if (apiResult.ResponseObject != null)
                {
                    var sierraError = apiResult.ResponseObject.ToObject<ErrorCode>();
                    result.Message = sierraError.Name;
                }
                else
                {
                    result.Message = apiResult.TransportError;
                }
            }
            return result;
        }

    }

}
