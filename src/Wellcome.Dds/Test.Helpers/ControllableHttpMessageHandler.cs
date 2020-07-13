﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Helpers
{
    /// <summary>
    /// Controllable HttpMessageHandler for unit testing HttpClient.
    /// </summary>
    public class ControllableHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage response;
        public List<string> CallsMade { get; }= new List<string>();

        public Action<HttpRequestMessage> Callback { get; private set; }

        /// <summary>
        /// Helper method to generate an HttpResponseMessage object
        /// </summary>
        public HttpResponseMessage GetResponseMessage(string content, HttpStatusCode httpStatusCode)
        {
            var httpContent = new StringContent(content);

            response = new HttpResponseMessage
            {
                StatusCode = httpStatusCode,
                Content = httpContent,
            };
            return response;
        }
        
        /// <summary>
        /// Set a pre-canned response 
        /// </summary>
        public void SetResponse(HttpResponseMessage response) => this.response = response;

        /// <summary>
        /// Register a callback when SendAsync called. Useful for verifying headers etc.
        /// </summary>
        /// <param name="callback">Function to call when SendAsync request made.</param>
        public void RegisterCallback(Action<HttpRequestMessage> callback) => Callback = callback;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallsMade.Add(request.RequestUri.ToString());
            Callback?.Invoke(request);

            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            tcs.SetResult(response);
            return tcs.Task;
        }
    }
}