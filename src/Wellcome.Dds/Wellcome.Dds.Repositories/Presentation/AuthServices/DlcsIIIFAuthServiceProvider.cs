namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public class DlcsIIIFAuthServiceProvider : IIIFAuthServiceProvider
    {
        const string BaseUri = "https://dlcs.io";
        
        protected override string GetAccessTokenServiceId()
        {
            return BaseUri + "/auth/2/token";
        }

        protected override string GetAcceptTermsAccessTokenServiceId()
        {
            return BaseUri + "/auth/2/token";
        }


        protected override string GetClickthroughLoginServiceId090()
        {
            return BaseUri + "/auth/2/clickthrough";
        }
        protected override string GetClickthroughLoginServiceId093()
        {
            return BaseUri + "/auth/2/clickthrough";
        }

        protected override string GetLogoutServiceId()
        {
            return BaseUri + "/auth/2/clickthrough/logout";
        }

        protected override string GetClinicalLoginServiceId()
        {
            return BaseUri + "/auth/2/clinicallogin";
        }

        protected override string GetCASTokenServiceId()
        {
            return BaseUri + "/auth/2/token";
        }

        protected override string GetRestrictedLoginServiceId090()
        {
            return BaseUri + "/auth/2/restrictedlogin";
        }
        protected override string GetRestrictedLoginServiceId093()
        {
            return BaseUri + "/auth/2/restrictedlogin";
        }
    }
}