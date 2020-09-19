using System;

namespace Wellcome.Dds.IIIFBuilding
{
    public class IIIFBuildStateException : Exception
    {
        public IIIFBuildStateException()
        {
        }

        public IIIFBuildStateException(string message)
            : base(message)
        {
        }

        public IIIFBuildStateException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}