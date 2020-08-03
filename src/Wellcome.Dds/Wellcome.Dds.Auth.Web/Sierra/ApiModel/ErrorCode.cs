namespace Wellcome.Dds.Auth.Web.Sierra.ApiModel
{
    public class ErrorCode
    {
        public int Code { get; set; }
        public int SpecificCode { get; set; }
        public int HttpStatus { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
