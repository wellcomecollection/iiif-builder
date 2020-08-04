using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Utils;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class SierraPinTest : IAuthenticationService
    {
        private HttpClient httpClient;
        private DdsOptions ddsOptions;

        public SierraPinTest(
            HttpClient httpClient,
            IOptions<DdsOptions> options)
        {
            this.httpClient = httpClient;
            ddsOptions = options.Value;
        }

        public async Task<AuthenticationResult> Authenticate(string username, string password)
        {
            try
            {
                username = HttpUtility.UrlEncode(username);
                password = HttpUtility.UrlEncode(password);
                var reqUrl = string.Format(ddsOptions.PinVerifyUrlFormat, username, password);
                var resText = await httpClient.GetStringAsync(reqUrl);
                var msg = HtmlUtils.TextOnly(resText).Trim();
                if ("RETCOD=0".Equals(msg))
                {
                    return new AuthenticationResult { Success = true };
                }
                return new AuthenticationResult 
                { 
                    Success = false, 
                    Message = FormatFailure(msg) 
                };
            }
            catch (Exception authEx)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    Message = "Authentication Exception: " + authEx.Message
                };
            }
        }

        private string FormatFailure(string patronApiMessage)
        {
            const string universalMessage = "Your username and/or password is incorrect.";
            // If the patron's record is found but the PIN is incorrect:
            // RETCOD=1
            // ERRNUM=4
            // ERRMSG=Invalid patron PIN

            // If the patron's record is found but there is no PIN in the record:
            // RETCOD=2
            // ERRNUM=4
            // ERRMSG=Invalid patron PIN

            // If the patron's record is not found:
            // ERRNUM=1
            // ERRMSG=Requested record not found

            // retaining this in case we want different messages/behaviours
            if (patronApiMessage.Contains("RETCOD=1") && patronApiMessage.Contains("ERRNUM=4"))
            {
                return universalMessage;
            }

            if (patronApiMessage.Contains("RETCOD=2") && patronApiMessage.Contains("ERRNUM=4"))
            {
                return universalMessage;
            }

            if (patronApiMessage.Contains("ERRNUM=1"))
            {
                return universalMessage;
            }

            return patronApiMessage;
        }
    }
}
