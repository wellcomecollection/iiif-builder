using IIIF.Auth;

namespace Wellcome.Dds.Repositories.Presentation.AuthServices
{
    public interface IAuthServiceProvider
    {
        AuthCookieService1[] GetAcceptTermsAuthServices();
        AuthCookieService1[] GetClinicalLoginServices();
        AuthCookieService1[] GetRestrictedLoginServices();
    }
}