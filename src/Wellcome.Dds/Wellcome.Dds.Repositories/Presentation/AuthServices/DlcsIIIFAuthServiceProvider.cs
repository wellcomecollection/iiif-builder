namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public class DlcsIIIFAuthServiceProvider : IIIFAuthServiceProvider
    {
        const string BaseUri = "https://iiif.wellcomecollection.org";
        
        protected override string GetClickthroughLoginServiceId()
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

        protected override string GetTokenServiceId()
        {
            return BaseUri + "/auth/token";
        }

        protected override string GetRestrictedLoginServiceId()
        {
            return BaseUri + "/auth/restrictedlogin";
        }
    }
}