using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.IIIF
{
    public interface IIIIFBuilder
    {
        public Task<BuildResult> Build(string identifier);
    }
}
