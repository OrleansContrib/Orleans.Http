using Microsoft.Owin;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace OrleansHttp
{
    public static class ExtensionMethods
    {
        public static Task ReturnJson(this IOwinContext context, object value)
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(JsonConvert.SerializeObject(value));
        }

        public static Task ReturnError(this IOwinContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 500;
            return context.Response.WriteAsync(ex.ToString());
        }


        public static Task ReturnUnauthorised(this IOwinContext context)
        {
            context.Response.StatusCode = 401;
            context.Response.ReasonPhrase = "Unauthorized";
            context.Response.Headers.Add("WWW-Authenticate", new string[] { "Basic realm=\"OrleansHttp\"" });
            return Task.FromResult(0);
        }

    }
}
