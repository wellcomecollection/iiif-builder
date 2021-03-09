namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public class DlcsIIIFAuthServiceProvider : IIIFAuthServiceProvider
    {
        const string BaseUri = "https://iiif.wellcomecollection.org";
        
        protected override string GetAccessTokenServiceId()
        {
            return BaseUri + "/auth/token";
        }

        protected override string GetAcceptTermsAccessTokenServiceId()
        {
            return BaseUri + "/auth/token";
        }


        protected override string GetClickthroughLoginServiceId090()
        {
            return BaseUri + "/auth/clickthrough";
        }
        protected override string GetClickthroughLoginServiceId093()
        {
            return BaseUri + "/auth/clickthrough";
        }

        protected override string GetLogoutServiceId()
        {
            return BaseUri + "/auth/clickthrough/logout";
        }

        protected override string GetClinicalLoginServiceId()
        {
            return BaseUri + "/auth/clinicallogin";
        }

        protected override string GetCASTokenServiceId()
        {
            return BaseUri + "/auth/token";
        }

        protected override string GetRestrictedLoginServiceId090()
        {
            return BaseUri + "/auth/restrictedlogin";
        }
        protected override string GetRestrictedLoginServiceId093()
        {
            return BaseUri + "/auth/restrictedlogin";
        }
    }
}