using System;
using System.Collections.Generic;
using System.Text;

namespace DlcsWebClient.Config
{
    public class DlcsOptions
    {
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int CustomerDefaultSpace { get; set; }
        public string ApiEntryPoint { get; set; }
        public string ResourceEntryPoint { get; set; }
        // The non-api root. Also, eventually, used for CRUD on IIIF resources directly, rather than API round the back.
    }
}
