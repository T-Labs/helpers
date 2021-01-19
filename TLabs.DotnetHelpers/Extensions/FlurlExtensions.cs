using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.AspNetCore.Builder;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TLabs.DotnetHelpers
{
    public static class FlurlExtensions
    {
        private static string _gatewayUrl;

        public static void UseFlurl(this IApplicationBuilder app, string gatewayUrl)
        {
            _gatewayUrl = gatewayUrl;

            FlurlHttp.Configure(settings =>
            {
                settings.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });

                // log info of error request
                settings.OnErrorAsync = async (FlurlCall call) =>
                {
                    const int maxLength = 500;
                    string body = call.RequestBody?.Substring(0, Math.Min(call.RequestBody?.Length ?? 0, maxLength));
                    string responseContent = null;
                    if (call.HttpResponseMessage?.Content != null)
                    {
                        responseContent = await call.HttpResponseMessage.Content.ReadAsStringAsync() ?? "";
                        responseContent = responseContent.Substring(0, Math.Min(responseContent.Length, maxLength));
                    }

                    Console.WriteLine($"Error: {call.Exception.Message}, " +
                        $"{(call.Duration.HasValue ? $"duration:{call.Duration}" : "")}" +
                        $"{(string.IsNullOrEmpty(body) ? "" : $"\nbody: {body}")}" +
                        $"{(string.IsNullOrEmpty(responseContent) ? "" : $"\nresponseContent: {responseContent}\n")}");
                };
            });
        }

        // Extend POST and PUT requests to specify response type in 1 call

        public static Task<T> PostJsonAsync<T>(this IFlurlRequest request, object data,
            CancellationToken cancellationToken = default,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return request.PostJsonAsync(data, cancellationToken, completionOption).ReceiveJson<T>();
        }

        public static Task<T> PutJsonAsync<T>(this IFlurlRequest request, object data,
            CancellationToken cancellationToken = default,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return request.PutJsonAsync(data, cancellationToken, completionOption).ReceiveJson<T>();
        }

        // Choose to use full url or send to gateway

        public static IFlurlRequest ExternalApi(this Url url) => new FlurlRequest(url);

        public static IFlurlRequest ExternalApi(this string url) => ExternalApi(new Url(url));

        public static IFlurlRequest InternalApi(this Url url) => new FlurlRequest(Url.Combine(_gatewayUrl, url));

        public static IFlurlRequest InternalApi(this string url) => InternalApi(new Url(url));

        // Wrap response in QueryResult

        public static async Task<QueryResult> GetQueryResult(this Task<IFlurlResponse> request)
        {
            try
            {
                await request;
                return QueryResult.CreateSucceeded();
            }
            catch (Exception e)
            {
                return QueryResult.CreateFailed(e.Message);
            }
        }

        public static async Task<QueryResult<T>> GetQueryResult<T>(this Task<T> request)
        {
            try
            {
                return QueryResult<T>.CreateSucceeded(await request);
            }
            catch (Exception e)
            {
                return QueryResult<T>.CreateFailed(e.Message);
            }
        }
    }
}
