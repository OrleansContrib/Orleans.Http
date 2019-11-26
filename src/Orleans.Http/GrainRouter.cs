using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Orleans.Http
{
    internal class GrainRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Dictionary<string, GrainInvoker>> _routes = new Dictionary<string, Dictionary<string, GrainInvoker>>(StringComparer.InvariantCultureIgnoreCase);

        public GrainRouter(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
        }

        public bool RegisterRoute(string pattern, string httpMethod, MethodInfo method, Type routeGrainProviderType)
        {
            if (this._routes.TryGetValue(pattern, out var grainRoutes))
            {
                if (grainRoutes.ContainsKey(httpMethod)) return false;

                grainRoutes[httpMethod] = new GrainInvoker(this._serviceProvider, method, routeGrainProviderType);
            }
            else
            {
                this._routes[pattern] = new Dictionary<string, GrainInvoker>(StringComparer.InvariantCultureIgnoreCase)
                {
                    [httpMethod] = new GrainInvoker(this._serviceProvider, method, routeGrainProviderType)
                };
            }

            return true;
        }

        public async Task Dispatch(HttpContext context)
        {
            var endpoint = (RouteEndpoint)context.GetEndpoint();
            var pattern = endpoint.RoutePattern;
            // At this point we are sure we have a pattern and an invoker since a route was match for that particular endpoint
            var allRoutes = this._routes[pattern.RawText];
            GrainInvoker invoker = default;

            if (!allRoutes.TryGetValue("*", out invoker))
            {
                invoker = allRoutes[context.Request.Method];
            }

            IGrain grain = await invoker.RouteGrainProvider.GetGrain(invoker.GrainType);

            if (grain == null)
            {
                // We only faw here if the grainId is mal formed
                //context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return;
            }

            await invoker.Invoke(grain, context);
        }
    }
}