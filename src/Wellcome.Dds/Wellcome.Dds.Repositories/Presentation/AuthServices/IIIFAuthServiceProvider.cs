using System.Collections.Generic;
using IIIF;
using IIIF.Auth;
using IIIF.LegacyInclusions;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public abstract class IIIFAuthServiceProvider : IAuthServiceProvider
    {
        public const string ClickthroughHeader = "Archival material less than 100 years old";
        public const string ClickthroughConfirmlabel = "Accept Terms and Open";

        // this can be a @lang multi-metadata value
        public const string ClickthroughLoginDescription = "This digitised " +
                    "archival material is free to access. By accepting Wellcome Library's terms and conditions, " +
                    "you agree to the following:<br><br>By viewing this and any subsequent archive " +
                    "material under 100 years old, I agree that I will use personal data on living " +
                    "persons for research purposes only. I will not use personal data to support " +
                    "decisions about the person who is the subject of the data, or in a way that " +
                    "causes substantial damage or distress to them.<br><br>" +
                    "<a href='https://wellcomelibrary.org/about-this-site/terms-and-conditions/'>" +
                    "Read Full Terms and Conditions</a>";

        public const string ClickthroughFailureHeader = "Terms not accepted";
        public const string ClickthroughFailureDescription = "You must accept the terms to view the content.";

        public const string ClinicalHeader = "Clinical material";
        public const string ClinicalLoginDescription =
            "Online access to clinical content is restricted to healthcare professionals. " +
            "Please contact Wellcome Images for further information: " +
            "<a href=\"mailto:images@wellcome.ac.uk\">images@wellcome.ac.uk</a>.<br><br>" +
            "If you are a healthcare professional and already have a Wellcome Trust account, " +
            "please log in.";
        public const string ClinicalFailureHeader = "Login failed";
        public const string ClinicalFailureDescription = "Your login attempt did not appear to be successful. Please try again.";

        public const string RestrictedHeader = "Restricted material";
        public const string RestrictedFailureDescription =
            "This image cannot be viewed online.<br><br>Wellcome Library members can request access " +
            "to restricted materials for viewing in the Library.<br><br>" +
            "";

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

        public AuthCookieService1[] GetAcceptTermsAuthServices()
        {
            var tokenServiceId = GetAcceptTermsAccessTokenServiceId();
            var services = new List<AuthCookieService1>();

            // for compatibility with current UV
            var clickthrough090Service = AuthCookieService1.NewClickthroughInstance();
            clickthrough090Service.Id = GetClickthroughLoginServiceId090();
            clickthrough090Service.Label = new MetaDataValue(ClickthroughHeader);
            clickthrough090Service.Description = new MetaDataValue(ClickthroughLoginDescription);
            clickthrough090Service.Service = GetCommonChildAuthServices(tokenServiceId);
            services.Add(clickthrough090Service);

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

            return services.ToArray();
        }
        public AuthCookieService1[] GetClinicalLoginServices()
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
            return new[] { clinicalLogin };
        }

        public AuthCookieService1[] GetRestrictedLoginServices()
        {
            var tokenServiceId = GetCASTokenServiceId();
            var services = new List<AuthCookieService1>();

            var external090Service = AuthCookieService1.NewExternalInstance();
            external090Service.Id = GetRestrictedLoginServiceId090();
            external090Service.Label = new MetaDataValue(RestrictedHeader);
            external090Service.Description = new MetaDataValue(RestrictedFailureDescription);
            external090Service.Service = GetCommonChildAuthServices(tokenServiceId);
            services.Add(external090Service);

            var external093Service = AuthCookieService1.NewExternalInstance();
            external093Service.Id = GetRestrictedLoginServiceId093();
            external093Service.Label = new MetaDataValue(RestrictedHeader);
            external093Service.FailureHeader = new MetaDataValue(RestrictedHeader);
            external093Service.FailureDescription = new MetaDataValue(RestrictedFailureDescription);
            external093Service.Service = GetCommonChildAuthServices(tokenServiceId);
            // confirmLabel? Not really appropriate; the client needs to provide the text for "cancel"...
            services.Add(external093Service);

            return services.ToArray();
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
