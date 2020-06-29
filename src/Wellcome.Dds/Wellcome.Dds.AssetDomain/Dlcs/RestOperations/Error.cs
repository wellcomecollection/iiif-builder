namespace Wellcome.Dds.AssetDomain.Dlcs.RestOperations
{
    public class Error
    {
        public int Status { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return Status + ": " + Message;
        }
    }
}
