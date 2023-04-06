using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stowage
{
   internal static class HttpExtensions
   {
      private static readonly HttpClient _httpClient = new HttpClient();

      public static async Task PostAsync(this string url, bool throwOnError = true)
      {
         var request = new HttpRequestMessage(HttpMethod.Post, url);
         HttpResponseMessage response = await _httpClient.SendAsync(request);
         if(throwOnError)
         {
            response.EnsureSuccessStatusCode();
         }

         string rjson = await response.Content.ReadAsStringAsync();
      }

      /// <summary>
      /// Ensures the given <paramref name="endpoint"/> ends in a trailing slash.
      /// </summary>
      /// <param name="endpoint"></param>
      /// <returns>A formatted <see cref="Uri"/></returns>
      public static Uri EnsureTrailingSlash(this Uri endpoint) =>
         endpoint.OriginalString.EndsWith(IOPath.PathSeparator)
            ? endpoint
            : new Uri($"{endpoint.OriginalString}{IOPath.PathSeparator}");

   }
}
