using System.Collections.Generic;
using IIIF;
using IIIF.Auth;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public interface IAuthServiceProvider
    {
        List<IService> GetAcceptTermsAuthServices();
        List<IService> GetClinicalLoginServices();
        List<IService> GetRestrictedLoginServices();
    }
}