using System.Collections.Generic;
using IIIF;
using IIIF.Auth;
using IIIF.Presentation.V2;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public abstract class IIIFAuthServiceProvider : IAuthServiceProvider
    {
        public const string ClickthroughHeader = "Content advisory";
        public const string ClickthroughConfirmlabel = "Accept Terms and Open";

        // this can be a @lang multi-metadata value
        public const string ClickthroughLoginDescription =
            "<p>This digitised material is free to access, but contains information or visuals that may:</p><ul>" +
            "<li>include personal details of living individuals</li>" +
            "<li>be upsetting or distressing</li>" +
            "<li>be explicit or graphic</li>" +
            "<li>include objects and images of objects decontextualised in a way that is offensive to the originating culture.</li>" +
            "</ul>" +
            "By viewing this material, we ask that you use the content lawfully, ethically and responsibly under the conditions set out in our " +
            "<a href=\"https://wellcomecollection.cdn.prismic.io/wellcomecollection/d4817da5-c71a-4151-81c4-83e39ad4f5b3_Wellcome+Collection_Access+Policy_Aug+2020.pdf\">Access Policy</a>.";

        public const string ClickthroughFailureHeader = "Terms not accepted";
        public const string ClickthroughFailureDescription = "You must accept the terms to view the content.";

        public const string ClinicalHeader = "Clinical material";

        public const string ClinicalLoginDescription =
            "<p>Online access to clinical content is restricted to healthcare professionals. Please contact the Collections team for further information: <a href='mailto:collections@wellcome.ac.uk'>collections@wellcome.ac.uk</a>.</p> <p>If you are a healthcare professional and already have a Wellcome Library account, please log in.</p>";
        public const string ClinicalFailureHeader = "Login failed";
        public const string ClinicalFailureDescription = "Your login attempt did not appear to be successful. Please try again.";

        public const string RestrictedHeader = "Restricted material";

        public const string RestrictedFailureDescription =
            "<p>This image cannot be viewed online.</p><p>Wellcome Library members can request access to restricted materials for viewing in the Library.</p>";

        public const string LogoutLabel = "Log out of Wellcome Library";


        protected abstract string GetAccessTokenServiceId();
        protected abstract string GetAcceptTermsAccessTokenServiceId();
        protected abstract string GetClickthroughLoginServiceId090();
        protected abstract string GetClickthroughLoginServiceId093();
        protected abstract string GetLogoutServiceId();
        protected abstract string GetClinicalLoginServiceId();
        protected abstract string GetRestrictedLoginServiceId090();
        protected abstract string GetRestrictedLoginServiceId093();
        protected abstract string GetCASTokenServiceId();

        public List<IService> GetAcceptTermsAuthServices()
        {
            var tokenServiceId = GetAcceptTermsAccessTokenServiceId();
            var services = new List<IService>();
            
            // Lets skip this for now...
            // for compatibility with current UV
            // var clickthrough090Service = AuthCookieService1.NewClickthroughInstance();
            // clickthrough090Service.Id = GetClickthroughLoginServiceId090();
            // clickthrough090Service.Label = new MetaDataValue(ClickthroughHeader);
            // clickthrough090Service.Description = new MetaDataValue(ClickthroughLoginDescription);
            // clickthrough090Service.Service = GetCommonChildAuthServices(tokenServiceId);
            // services.Add(clickthrough090Service);

            // for UV compliant with 0.9.3
            var clickthrough093Service = AuthCookieService1.NewClickthroughInstance();
            clickthrough093Service.Id = GetClickthroughLoginServiceId093();
            clickthrough093Service.Label = new MetaDataValue(ClickthroughHeader);
            clickthrough093Service.Header = new MetaDataValue(ClickthroughHeader);
            clickthrough093Service.Description = new MetaDataValue(ClickthroughLoginDescription);
            clickthrough093Service.ConfirmLabel = new MetaDataValue(ClickthroughConfirmlabel);
            clickthrough093Service.FailureHeader = new MetaDataValue(ClickthroughFailureHeader);
            clickthrough093Service.FailureDescription = new MetaDataValue(ClickthroughFailureDescription);
            clickthrough093Service.Service = GetCommonChildAuthServices(tokenServiceId);
            services.Add(clickthrough093Service);

            return services;
        }
        public List<IService> GetClinicalLoginServices()
        {
            var clinicalLogin = AuthCookieService1.NewLoginInstance();
            clinicalLogin.Id = GetClinicalLoginServiceId();
            clinicalLogin.ConfirmLabel = new MetaDataValue("LOGIN");
            clinicalLogin.Label = new MetaDataValue(ClinicalHeader);
            clinicalLogin.Header = new MetaDataValue(ClinicalHeader);
            clinicalLogin.Description = new MetaDataValue(ClinicalLoginDescription);
            clinicalLogin.FailureHeader = new MetaDataValue(ClinicalFailureHeader);
            clinicalLogin.FailureDescription = new MetaDataValue(ClinicalFailureDescription);
            clinicalLogin.Service = GetCommonChildAuthServices(GetCASTokenServiceId());
            return new List<IService>() {clinicalLogin};
        }

        public List<IService> GetRestrictedLoginServices()
        {
            var tokenServiceId = GetCASTokenServiceId();
            var services = new List<IService>();

            // var external090Service = AuthCookieService1.NewExternalInstance();
            // external090Service.Id = GetRestrictedLoginServiceId090();
            // external090Service.Label = new MetaDataValue(RestrictedHeader);
            // external090Service.Description = new MetaDataValue(RestrictedFailureDescription);
            // external090Service.Service = GetCommonChildAuthServices(tokenServiceId);
            // services.Add(external090Service);

            var external093Service = AuthCookieService1.NewExternalInstance();
            external093Service.Id = GetRestrictedLoginServiceId093();
            external093Service.Label = new MetaDataValue(RestrictedHeader);
            external093Service.FailureHeader = new MetaDataValue(RestrictedHeader);
            external093Service.FailureDescription = new MetaDataValue(RestrictedFailureDescription);
            external093Service.Service = GetCommonChildAuthServices(tokenServiceId);
            // confirmLabel? Not really appropriate; the client needs to provide the text for "cancel"...
            services.Add(external093Service);

            return services;
        }

        private List<IService> GetCommonChildAuthServices(string tokenServiceId)
        {
            var commonChildServices = new List<IService>
            {
                new AuthTokenService1
                {
                    Id = tokenServiceId
                },           
                new AuthLogoutService1
                {
                    Id = GetLogoutServiceId(),
                    Label = new MetaDataValue(LogoutLabel)
                }
            };
            return commonChildServices;
        }


    }
}
