namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class SierraRestApiOptions
    {
        public string TokenEndPoint { get; set; }
        public string Scope { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string PatronValidateUrl { get; set; }
        public string PatronFindUrl { get; set; }
        public string PatronGetUrl { get; set; }
    }
}
