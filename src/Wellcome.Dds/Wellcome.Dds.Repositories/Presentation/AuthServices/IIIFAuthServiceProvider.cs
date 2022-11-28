using System.Collections.Generic;
using IIIF;
using IIIF.Auth.V1;
using IIIF.Auth.V2;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public abstract class IIIFAuthServiceProvider : IAuthServiceProvider
    {
        // TODO: https://github.com/wellcomecollection/platform/issues/5634
        private const string ClickthroughHeader = "Content advisory";
        private const string ClickthroughConfirmlabel = "Accept Terms and Open";

        // this can be a @lang multi-metadata value
        private const string ClickthroughLoginDescription =
            "<p>This digitised material is free to access, but contains information or visuals that may:</p><ul>" +
            "<li>include personal details of living individuals</li>" +
            "<li>be upsetting or distressing</li>" +
            "<li>be explicit or graphic</li>" +
            "<li>include objects and images of objects decontextualised in a way that is offensive to the originating culture.</li>" +
            "</ul>" +
            "By viewing this material, we ask that you use the content lawfully, ethically and responsibly under the conditions set out in our " +
            "<a href=\"https://wellcomecollection.cdn.prismic.io/wellcomecollection/d4817da5-c71a-4151-81c4-83e39ad4f5b3_Wellcome+Collection_Access+Policy_Aug+2020.pdf\">Access Policy</a>.";

        private const string ClickthroughFailureHeader = "Terms not accepted";
        private const string ClickthroughFailureDescription = "You must accept the terms to view the content.";

        private const string ClinicalHeader = "Clinical material";

        private const string ClinicalLoginDescription =
            "<p>Online access to clinical content is restricted to healthcare professionals. Please contact the Collections team for further information: <a href='mailto:collections@wellcome.ac.uk'>collections@wellcome.ac.uk</a>.</p> <p>If you are a healthcare professional and already have a Wellcome Collection account, please log in.</p>";
        private const string ClinicalFailureHeader = "Login failed";
        private const string ClinicalFailureDescription = "Your login attempt did not appear to be successful. Please try again.";

        private const string RestrictedHeader = "Restricted material";

        private const string RestrictedFailureDescription =
            "<p>This image cannot be viewed online.</p><p>Wellcome Collection members can request access to restricted materials for viewing in the Library.</p>";

        private const string LogoutLabel = "Log out of Wellcome Collection";


        protected abstract string GetClickthroughLoginServiceId();
        protected abstract string GetLogoutServiceId();
        protected abstract string GetClinicalLoginServiceId();
        protected abstract string GetRestrictedLoginServiceId();
        protected abstract string GetTokenServiceId();

        public IService GetAcceptTermsAuthServicesV1()
        {
            // for UV compliant with 0.9.3
            var clickthroughV1Service = AuthCookieService.NewClickthroughInstance();
            clickthroughV1Service.Id = GetClickthroughLoginServiceId();
            clickthroughV1Service.Label = new MetaDataValue(ClickthroughHeader);
            clickthroughV1Service.Header = new MetaDataValue(ClickthroughHeader);
            clickthroughV1Service.Description = new MetaDataValue(ClickthroughLoginDescription);
            clickthroughV1Service.ConfirmLabel = new MetaDataValue(ClickthroughConfirmlabel);
            clickthroughV1Service.FailureHeader = new MetaDataValue(ClickthroughFailureHeader);
            clickthroughV1Service.FailureDescription = new MetaDataValue(ClickthroughFailureDescription);
            clickthroughV1Service.Service = GetCommonChildAuthServicesV1();
            return clickthroughV1Service;
        }

        public IService GetAcceptTermsAuthServicesV2()
        {
            return new AuthAccessService2
            {
                Id = AsV2(GetClickthroughLoginServiceId()),
                Profile = AuthAccessService2.InteractiveProfile,
                Label = new LanguageMap("en", ClickthroughHeader),
                Header = new LanguageMap("en", ClickthroughHeader),
                Description = new LanguageMap("en", ClickthroughLoginDescription),
                ConfirmLabel = new LanguageMap("en", ClickthroughConfirmlabel),
                FailureHeader = new LanguageMap("en", ClickthroughFailureHeader),
                FailureDescription = new LanguageMap("en", ClickthroughFailureDescription),
                Service = GetCommonChildAuthServicesV2()
            };
        }
        
        public IService GetClinicalLoginServicesV1()
        {
            var clinicalLoginV1Service = AuthCookieService.NewLoginInstance();
            clinicalLoginV1Service.Id = GetClinicalLoginServiceId();
            clinicalLoginV1Service.ConfirmLabel = new MetaDataValue("LOGIN");
            clinicalLoginV1Service.Label = new MetaDataValue(ClinicalHeader);
            clinicalLoginV1Service.Header = new MetaDataValue(ClinicalHeader);
            clinicalLoginV1Service.Description = new MetaDataValue(ClinicalLoginDescription);
            clinicalLoginV1Service.FailureHeader = new MetaDataValue(ClinicalFailureHeader);
            clinicalLoginV1Service.FailureDescription = new MetaDataValue(ClinicalFailureDescription);
            clinicalLoginV1Service.Service = GetCommonChildAuthServicesV1();
            return clinicalLoginV1Service;
        }

        public IService GetClinicalLoginServicesV2()
        {
            return new AuthAccessService2
            {
                Id = AsV2(GetClinicalLoginServiceId()),
                Profile = AuthAccessService2.InteractiveProfile,
                Label = new LanguageMap("en", ClinicalHeader),
                Header = new LanguageMap("en", ClinicalHeader),
                Description = new LanguageMap("en", ClinicalLoginDescription),
                ConfirmLabel = new LanguageMap("en", "LOGIN"),
                FailureHeader = new LanguageMap("en", ClinicalFailureDescription),
                FailureDescription = new LanguageMap("en", ClinicalFailureDescription),
                Service = GetCommonChildAuthServicesV2()
            };
        }
        
        public IService GetRestrictedLoginServicesV1()
        {
            var externalServiceV1 = AuthCookieService.NewExternalInstance();
            externalServiceV1.Id = GetRestrictedLoginServiceId();
            externalServiceV1.Label = new MetaDataValue(RestrictedHeader);
            externalServiceV1.FailureHeader = new MetaDataValue(RestrictedHeader);
            externalServiceV1.Description = new MetaDataValue(RestrictedFailureDescription);
            externalServiceV1.FailureDescription = new MetaDataValue(RestrictedFailureDescription);
            externalServiceV1.Service = GetCommonChildAuthServicesV1();
            // confirmLabel? Not really appropriate; the client needs to provide the text for "cancel"...
            return externalServiceV1;
        }

        public IService GetRestrictedLoginServicesV2()
        {
            return new AuthAccessService2
            {
                Id = AsV2(GetRestrictedLoginServiceId()),
                Profile = AuthAccessService2.ExternalProfile,
                Label = new LanguageMap("en", RestrictedHeader),
                FailureHeader = new LanguageMap("en", RestrictedHeader),
                Description = new LanguageMap("en", RestrictedFailureDescription),
                FailureDescription = new LanguageMap("en", RestrictedFailureDescription),
                Service = GetCommonChildAuthServicesV2()
            };
        }

        private List<IService> GetCommonChildAuthServicesV1()
        {
            return new List<IService>
            {
                new AuthTokenService
                {
                    Id = GetTokenServiceId()
                },           
                new AuthLogoutService
                {
                    Id = GetLogoutServiceId(),
                    Label = new MetaDataValue(LogoutLabel)
                }
            };
        }

        private List<IService> GetCommonChildAuthServicesV2()
        {
            return new List<IService>
            {
                new AuthTokenService2
                {
                    Id = AsV2(GetTokenServiceId())
                },
                new AuthLogoutService2
                {
                    Id = AsV2(GetLogoutServiceId()),
                    Label = new LanguageMap("en", LogoutLabel)
                }
            };
        }

        private string AsV2(string originalUrl)
        {
            return originalUrl.ReplaceFirst("/auth/", "/auth/v2/");
        }
    }
}
