using IIIF;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public interface IAuthServiceProvider
    {
        // original implementation (renamed ..V1)
        IService GetAcceptTermsAuthServicesV1();
        IService GetClinicalLoginServicesV1();
        IService GetRestrictedLoginServicesV1();
        
        // Auth V2 providers
        // Later we can reorganise this and support more variety in auth services
        IService GetAcceptTermsAuthServicesV2();
        IService GetClinicalLoginServicesV2();
        IService GetRestrictedLoginServicesV2();
    }
}