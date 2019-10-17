using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal static class RouteDelegateBuilder
    {
        public static RequestDelegate BuildRouteDelegate(
            IClusterClient clusterClient,
            string prefix,
            string topLevelPattern,
            string grainTypeName,
            RouteAttribute attr)
        {
            return context =>
            {

                return Task.CompletedTask;
            };
        }

        public static RequestDelegate BuildRouteDelegate(
            IClusterClient clusterClient,
            string prefix,
            string topLevelPattern,
            string grainTypeName,
            MethodAttribute attr)
        {
            return context =>
            {

                return Task.CompletedTask;
            };
        }
    }
}