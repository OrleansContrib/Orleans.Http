using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Orleans.Http
{
    internal class GrainRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly Dictionary<string, Dictionary<string, GrainInvoker>> _routes = new Dictionary<string, Dictionary<string, GrainInvoker>>(StringComparer.InvariantCultureIgnoreCase);

        public GrainRouter(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            this._serviceProvider = serviceProvider;
            this._logger = loggerFactory.CreateLogger<GrainRouter>();
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

            IGrain grain = null;
            try
            {
                grain = await invoker.RouteGrainProvider.GetGrain(invoker.GrainType);
            }
            catch(Exception ex)
            {
                this._logger.LogError(ex, "");
            }

            if (grain == null)
            {
                //Check if status is set to OK and change to internal server error, the invoker's RouteGrainProvider implementation may handle this otherwise
                if (context.Response.StatusCode == (int)System.Net.HttpStatusCode.OK)
                {
                    this._logger.LogError($"Failure getting grain '{invoker.GrainType.FullName}' for route '{pattern.RawText}' with RouteGrainProvider '{invoker.RouteGrainProvider.GetType()}' and was unhandled");
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                }
                return;
            }

            await invoker.Invoke(grain, context);
        }
    }
}