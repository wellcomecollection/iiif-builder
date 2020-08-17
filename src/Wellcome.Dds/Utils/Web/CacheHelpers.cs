using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.Net;

namespace Utils.Web
{
    public static class CacheHelpers
    {
        public static void AppendStandardNoCacheHeaders(this HttpResponse response)
        {
            response.Headers.Append("Cache-Control", "no-cache, s-maxage=0, max-age=0");
            response.Headers.Append("Pragma", "no-cache");
            response.Headers.Append("Expires", DateTime.Now.Date.ToString("r", DateTimeFormatInfo.InvariantInfo));
        }

        public static void CacheForDays(this HttpResponse response, int days)
        {
            response.CacheForSeconds(days * 86400);
        }

        public static void CacheForHours(this HttpResponse response, int hours)
        {
            response.CacheForSeconds(hours * 3600);
        }

        public static void CacheForMinutes(this HttpResponse response, int minutes)
        {
            response.CacheForSeconds(minutes * 60);
        }

        public static void CacheForSeconds(this HttpResponse response, int seconds)
        {
            const string template = "public, s-maxage={0}, max-age={0}";
            response.Headers.Append("Cache-Control", String.Format(template, seconds));
        }
    }
}
