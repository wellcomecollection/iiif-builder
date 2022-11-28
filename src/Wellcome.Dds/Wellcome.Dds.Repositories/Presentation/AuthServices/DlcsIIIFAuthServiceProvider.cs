namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public class DlcsIIIFAuthServiceProvider : IIIFAuthServiceProvider
    {
        private readonly string dlcsEntryPoint;
        
        public DlcsIIIFAuthServiceProvider(string dlcsEntryPoint)
        {
            this.dlcsEntryPoint = dlcsEntryPoint;
        }
        
        protected override string GetClickthroughLoginServiceId()
        {
            return $"{dlcsEntryPoint}auth/clickthrough";
        }

        protected override string GetLogoutServiceId()
        {
            return $"{dlcsEntryPoint}auth/clickthrough/logout";
        }

        protected override string GetClinicalLoginServiceId()
        {
            return $"{dlcsEntryPoint}auth/clinicallogin";
        }

        protected override string GetTokenServiceId()
        {
            return $"{dlcsEntryPoint}auth/token";
        }

        protected override string GetRestrictedLoginServiceId()
        {
            return $"{dlcsEntryPoint}auth/restrictedlogin";
        }
    }
}