using System.Collections.Generic;
using IIIF;
using IIIF.Auth.V1;
using IIIF.Auth.V2;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;
using Utils;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public class IIIFAuthServiceProvider : IAuthServiceProvider
    {
        private readonly string dlcsEntryPoint;
        private readonly UriPatterns uriPatterns;
        
        public IIIFAuthServiceProvider(string dlcsEntryPoint, UriPatterns uriPatterns)
        {
            this.dlcsEntryPoint = dlcsEntryPoint;
            this.uriPatterns = uriPatterns;
        }
        
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

        public IService GetAcceptTermsAuthServicesV1()
        {
            // for UV compliant with 0.9.3
            var clickthroughV1Service = AuthCookieService.NewClickthroughInstance();
            clickthroughV1Service.Id = uriPatterns.DlcsClickthroughLoginServiceId(dlcsEntryPoint);
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
                Id = uriPatterns.DlcsClickthroughLoginServiceV2Id(dlcsEntryPoint),
                Profile = AuthAccessService2.ActiveProfile,
                Label = new LanguageMap("en", ClickthroughHeader),
                Heading = new LanguageMap("en", ClickthroughHeader),
                Note = new LanguageMap("en", ClickthroughLoginDescription),
                ConfirmLabel = new LanguageMap("en", ClickthroughConfirmlabel),
                Service = GetCommonChildAuthServicesV2()
            };
        }
        
        public IService GetClinicalLoginServicesV1()
        {
            var clinicalLoginV1Service = AuthCookieService.NewLoginInstance();
            clinicalLoginV1Service.Id = uriPatterns.DlcsClinicalLoginServiceId(dlcsEntryPoint);
            clinicalLoginV1Service.ConfirmLabel = new MetaDataValue("LOGIN");
            clinicalLoginV1Service.Label = new MetaDataValue(ClinicalHeader);
            clinicalLoginV1Service.Header = new MetaDataValue(ClinicalHeader);
            clinicalLoginV1Service.Description = new MetaDataValue(ClinicalLoginDescription);
            clinicalLoginV1Service.FailureHeader = new MetaDataValue(ClinicalFailureHeader);
            clinicalLoginV1Service.FailureDescription = new MetaDataValue(ClinicalFailureDescription);
            clinicalLoginV1Service.Service = GetCommonChildAuthServicesV1();
            return clinicalLoginV1Service;
        }

        public IService GetLoginServicesV2()
        {
            return new AuthAccessService2
            {
                Id = uriPatterns.DlcsLoginServiceV2Id(dlcsEntryPoint),
                Profile = AuthAccessService2.ActiveProfile,
                Label = new LanguageMap("en", ClinicalHeader),
                Heading = new LanguageMap("en", ClinicalHeader),
                Note = new LanguageMap("en", ClinicalLoginDescription),
                ConfirmLabel = new LanguageMap("en", "LOGIN"),
                Service = GetCommonChildAuthServicesV2()
            };
        }
        
        public IService GetRestrictedLoginServicesV1()
        {
            var externalServiceV1 = AuthCookieService.NewExternalInstance();
            externalServiceV1.Id = uriPatterns.DlcsRestrictedLoginServiceId(dlcsEntryPoint);
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
                Id = uriPatterns.DlcsRestrictedLoginServiceV2Id(dlcsEntryPoint),
                Profile = AuthAccessService2.ExternalProfile,
                Label = new LanguageMap("en", RestrictedHeader),
                Heading = new LanguageMap("en", RestrictedHeader),
                Note = new LanguageMap("en", RestrictedFailureDescription),
                Service = GetCommonChildAuthServicesV2()
            };
        }

        public AuthProbeService2 GetClickthroughProbeService(string assetIdentifier)
        {
            return new AuthProbeService2
            {
                Id = uriPatterns.DlcsProbeServiceV2(dlcsEntryPoint, assetIdentifier)
                // ErrorHeading = new LanguageMap("en", ClickthroughFailureHeader),
                // ErrorNote = new LanguageMap("en", ClickthroughFailureDescription)
            };
        }
        
        public AuthProbeService2 GetLoginProbeService(string assetIdentifier)
        {
            return new AuthProbeService2
            {
                Id = uriPatterns.DlcsProbeServiceV2(dlcsEntryPoint, assetIdentifier)
                // ErrorHeading = new LanguageMap("en", ClinicalFailureDescription),
                // ErrorNote = new LanguageMap("en", ClinicalFailureDescription),
            };
        }
        
        public AuthProbeService2 GetRestrictedProbeService(string assetIdentifier)
        {
            return new AuthProbeService2
            {
                Id = uriPatterns.DlcsProbeServiceV2(dlcsEntryPoint, assetIdentifier)
                // ErrorHeading = new LanguageMap("en", RestrictedHeader),
                // ErrorNote = new LanguageMap("en", RestrictedFailureDescription)
            };
        }
        
        private List<IService> GetCommonChildAuthServicesV1()
        {
            return new List<IService>
            {
                new AuthTokenService
                {
                    Id = uriPatterns.DlcsTokenServiceId(dlcsEntryPoint)
                },           
                new AuthLogoutService
                {
                    Id = uriPatterns.DlcsLogoutServiceId(dlcsEntryPoint),
                    Label = new MetaDataValue(LogoutLabel)
                }
            };
        }

        private List<IService> GetCommonChildAuthServicesV2()
        {
            return new List<IService>
            {
                new AuthAccessTokenService2
                {
                    Id = uriPatterns.DlcsTokenServiceV2Id(dlcsEntryPoint)
                },
                new AuthLogoutService2
                {
                    Id = uriPatterns.DlcsLogoutServiceV2Id(dlcsEntryPoint),
                    Label = new LanguageMap("en", LogoutLabel)
                }
            };
        }
    }
}
