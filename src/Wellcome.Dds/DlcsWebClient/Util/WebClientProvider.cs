using System;
using System.Net;

namespace DlcsWebClient.Util
{
    /// <summary>
    /// TODO: This must be replaced with an injected HttpClient. 
    /// This class goes, and Dlcs.cs gets a DI HttpClient.
    /// </summary>
    public class WebClientProvider
    {
        public static WebClient GetWebClient(int timeout = 60000)
        {
            // All the proxy handling code has been removed
            // TODO - refactor for httpclient, injected and configured properly


            return new WebClientWithTimeout(timeout);
        }

        private class WebClientWithTimeout : WebClient
        {
            /// <summary>
            /// Time in milliseconds
            /// </summary>
            private int Timeout { get; set; }

            public WebClientWithTimeout() : this(60000) { }

            public WebClientWithTimeout(int timeout)
            {
                Timeout = timeout;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.Timeout = Timeout;
                }
                return request;
            }
        }
    }
}