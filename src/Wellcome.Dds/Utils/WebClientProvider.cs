using System;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;

namespace Utils
{
    /// <summary>
    /// Provides a System.Net.WebClient that is configured with a proxy server, if configuration demands it.
    /// 
    /// 
    /// HAVE COMMENTED OUT THE PROXY! This needs to be replaced with HttpClient
    /// </summary>
    public class WebClientProvider
    {
        //private static readonly string ProxyAddress = ConfigurationManager.AppSettings["ProxyAddress"];
        //private static readonly string ProxyPort = ConfigurationManager.AppSettings["ProxyPort"];
        //private static readonly string ProxyDomain = ConfigurationManager.AppSettings["ProxyDomain"];
        //private static readonly string ProxyUsername = ConfigurationManager.AppSettings["ProxyUsername"];
        //private static readonly string ProxyPassword = ConfigurationManager.AppSettings["ProxyPassword"];

        //private static readonly int ProxyPortNumber;

        //static WebClientProvider()
        //{
        //    Int32.TryParse(ProxyPort, out ProxyPortNumber);
        //}

        public static WebClient GetWebClient(int timeout = 60000,
            bool withProxy = true,
            RequestCacheLevel requestCacheLevel = RequestCacheLevel.Default)
        {
            // System.Net.ServicePointManager.Expect100Continue = !DisableExpect100Continue;
            var wc = new WebClientWithTimeout(timeout, requestCacheLevel);
            return wc;
            //if (!withProxy)
            //{
            //    return wc;
            //}
            //if ((ProxyAddress ?? "").Trim().Length == 0) return wc;
            //string a = ProxyAddress.Trim();
            //if (ProxyPortNumber > 0) a += ":" + ProxyPort;
            //var proxy = new WebProxy { Address = new Uri(a) };
            //if ((ProxyUsername ?? "").Trim().Length > 0)
            //{
            //    if ((ProxyDomain ?? "").Trim().Length > 0)
            //        proxy.Credentials = new NetworkCredential(ProxyUsername.Trim(), ProxyPassword, ProxyDomain.Trim());
            //    else
            //        proxy.Credentials = new NetworkCredential(ProxyUsername.Trim(), ProxyPassword);
            //}
            //wc.Proxy = proxy;
            //return wc;
        }

        private class WebClientWithTimeout : WebClient
        {
            /// <summary>
            /// Time in milliseconds
            /// </summary>
            public int Timeout { get; set; }

            public RequestCacheLevel RequestCacheLevel { get; set; }

            public WebClientWithTimeout() : this(60000) { }

            public WebClientWithTimeout(int timeout) : this(timeout, RequestCacheLevel.Default)
            {
                this.Timeout = timeout;
            }

            public WebClientWithTimeout(int timeout, RequestCacheLevel requestCacheLevel)
            {
                this.Timeout = timeout;
                this.RequestCacheLevel = requestCacheLevel;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.CachePolicy = new RequestCachePolicy(RequestCacheLevel);
                    request.Timeout = this.Timeout;
                }
                return request;
            }
        }

        public static void DebugHeaders(WebHeaderCollection headers)
        {
            Debug.WriteLine("===== start ====================");
            for (int i = 0; i < headers.Count; ++i)
                Debug.WriteLine(headers.Keys[i] + ": " + headers[i]);
            Debug.WriteLine("===== end   ====================");
        }
    }
}

