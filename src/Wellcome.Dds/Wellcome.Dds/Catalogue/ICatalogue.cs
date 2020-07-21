using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.Catalogue
{
    public interface ICatalogue
    {
        Work GetWork(string identifier);
    }
}
