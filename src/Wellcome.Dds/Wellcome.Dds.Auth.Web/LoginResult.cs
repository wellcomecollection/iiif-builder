namespace Wellcome.Dds.Auth.Web
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public Roles Roles { get; set; }
        public string Message { get; set; }
    }
}
