using System;

namespace Wellcome.Dds.AssetDomain.Dlcs.RestOperations
{
    public class Error
    {
        public Error(int status, string message)
        {
            Status = status;
            Message = message;
        }

        public int Status { get; set; }
        public string Message { get; set; }

        public Exception? Exception { get; set; }

        public override string ToString()
        {
            return Status + ": " + Message;
        }
    }
}
