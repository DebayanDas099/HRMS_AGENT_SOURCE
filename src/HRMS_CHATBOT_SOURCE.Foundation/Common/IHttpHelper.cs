using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace HRMS_CHATBOT_SOURCE.Foundation
{
    public interface IHttpHelper
    {
        Task<HttpResponseMessage> SendAsync(string httpClientName, HttpMethod method, string requestUri, MediaTypeHeaderValue contentType, Dictionary<string, string>? headers, Dictionary<string, string>? queryParams, HttpContent? content);
        Task<HttpResponseMessage> GetAsync(string httpClientName, string requestUri, MediaTypeHeaderValue contentType, Dictionary<string, string>? headers, Dictionary<string, string>? queryParams);
        Task<HttpResponseMessage> PostAsync(string httpClientName, string requestUri, MediaTypeHeaderValue contentType, Dictionary<string, string>? headers, Dictionary<string, string>? queryParams, HttpContent? content);
        Task<HttpResponseMessage> PutAsync(string httpClientName, string requestUri, MediaTypeHeaderValue contentType, Dictionary<string, string>? headers, Dictionary<string, string>? queryParams, HttpContent? content);
        Task<HttpResponseMessage> DeleteAsync(string httpClientName, string requestUri, MediaTypeHeaderValue contentType, Dictionary<string, string>? headers, Dictionary<string, string>? queryParams, HttpContent? content);
    }
}
