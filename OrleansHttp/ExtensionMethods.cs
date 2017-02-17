using Microsoft.Owin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace OrleansHttp
{
    public static class ExtensionMethods
    {
        public static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings{TypeNameHandling = TypeNameHandling.Auto};
        public static Task ReturnJson(this IOwinContext context, object value)
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(JsonConvert.SerializeObject(value, jsonSerializerSettings));
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
        public static MethodInfo GetSubImplMethod(this System.Type type, string methodName, ref HashSet<Type> exclude)
        {
            MethodInfo method = type.GetMethod(methodName);
            if (method != null)
                return method;
            foreach (var parent_type in type.GetInterfaces())
            {
                if (!exclude.Contains(parent_type))
                {
                    exclude.Add(parent_type);
                    method = parent_type.GetSubImplMethod(methodName, ref exclude);
                    if (method != null)
                        break;
                }
            }
            return method;
        }
        public static MethodInfo GetImpMethod(this System.Type type, string methodName)
        {
            HashSet<Type> checkedType = new HashSet<Type>();
            var method = type.GetSubImplMethod(methodName, ref checkedType);
            return method;
        }
    }
}
