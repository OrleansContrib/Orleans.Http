using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orleans.ApplicationParts;
using Orleans.Metadata;
using System.Linq;
using Orleans.Http.Abstractions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing.Patterns;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Orleans.Http
{
    public static class HostingExtensions
    {
        private static readonly List<Type> _methodAttributeTypes = new List<Type>{
            typeof(HttpGetAttribute),
            typeof(HttpPostAttribute),
            typeof(HttpPutAttribute),
            typeof(HttpDeleteAttribute)
        };

        private static readonly Type _routeAttributeType = typeof(RouteAttribute);

        private static readonly List<Type> _parameterAttributeTypes = new List<Type>{
            typeof(FromQueryAttribute),
            typeof(FromBodyAttribute)
        };

        public static IServiceCollection AddGrainRouter(this IServiceCollection services)
        {
            return services.AddSingleton<GrainRouteDispatcher>();
        }

        public static IEndpointRouteBuilder MapGrains(this IEndpointRouteBuilder routes, string prefix = "")
        {
            // Normalize Prefix
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                prefix = $"{prefix}/";
            }
            else
            {
                prefix = "/";
            }

            var sp = routes.ServiceProvider;

            var dispatcher = sp.GetRequiredService<GrainRouteDispatcher>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Orleans.Http.GrainRouteMapper");
            var appPartsMgr = sp.GetRequiredService<IApplicationPartManager>();

            var grainInterfaceFeature = appPartsMgr.CreateAndPopulateFeature<GrainInterfaceFeature>();

            // First scan which grain types should be mapped (i.e. have the routing attributes)
            logger.LogInformation("Scanning grain types to be mapped to routes...");
            var grainTypesToMap = DiscoverGrainTypesToMap(grainInterfaceFeature);

            int routesCreated = 0;
            // Map each grain type to a route based on the attributes
            foreach (var grainType in grainTypesToMap)
            {
                routesCreated += MapGrainToRoute(routes, grainType, prefix, dispatcher, logger);
            }

            logger.LogInformation($"{routesCreated} route(s) were created for grains.");
            return routes;
        }

        private static int MapGrainToRoute(IEndpointRouteBuilder routes, Type grainType, string prefix, GrainRouteDispatcher dispatcher, ILogger logger)
        {
            logger.LogInformation($"Mapping routes for grain '{grainType.FullName}'...");

            // First we check if the grain has the [RouteAttribute] applied to the interface so we capture its info for as a prefix
            var topRouteAttr = (RouteAttribute)grainType.GetCustomAttributes(false).Where(attr => attr.GetType() == _routeAttributeType).SingleOrDefault();
            string topLevelPattern = string.Empty;
            string topLevelRouteName = string.Empty;

            if (!string.IsNullOrWhiteSpace(topRouteAttr?.Pattern))
            {
                // Normalize top-level pattern
                if (!string.IsNullOrWhiteSpace(topRouteAttr.Pattern))
                {
                    topLevelPattern = $"{topRouteAttr.Pattern}/";
                }

                topLevelRouteName = string.IsNullOrWhiteSpace(topRouteAttr.Name) ? topLevelPattern : topRouteAttr.Pattern;
            }

            // Then we get only the methods that has any of our supported attributes applied to it
            var methods = grainType.GetMethods().Where(m => m.GetCustomAttributes(false)
                            .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))).ToArray();

            int routesRegistered = 0;
            foreach (var method in methods)
            {
                var methodAttributes = method.GetCustomAttributes(false)
                    .Where(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()));

                foreach (var attribute in methodAttributes)
                {
                    Func<RoutePattern, RequestDelegate, IEndpointConventionBuilder> mapFunc = default;
                    var httpMethod = string.Empty;
                    RoutePattern routePattern = default;
                    RequestDelegate requestDelegate = default;

                    if (attribute is RouteAttribute routeAttr)
                    {
                        routePattern = RoutePatternBuilder.BuildRoutePattern(prefix, topLevelPattern, grainType.FullName, method.Name, routeAttr.Pattern);
                        httpMethod = "*";
                        mapFunc = routes.Map;
                        // requestDelegate = RouteDelegateBuilder.BuildRouteDelegate(clusterClient, routePattern, method);
                    }
                    else if (attribute is MethodAttribute methodAttr)
                    {
                        routePattern = RoutePatternBuilder.BuildRoutePattern(prefix, topLevelPattern, grainType.FullName, method.Name, methodAttr.Pattern);
                        httpMethod = methodAttr.Method;
                        // requestDelegate = RouteDelegateBuilder.BuildRouteDelegate(clusterClient, routePattern, grainType.FullName, methodAttr);

                        Func<string, RequestDelegate, IEndpointConventionBuilder> methodMapFunc = default;

                        switch (methodAttr.Method)
                        {
                            case "GET":
                                methodMapFunc = routes.MapGet;
                                break;
                            case "POST":
                                methodMapFunc = routes.MapPost;
                                break;
                            case "PUT":
                                methodMapFunc = routes.MapPut;
                                break;
                            case "DELETE":
                                methodMapFunc = routes.MapDelete;
                                break;
                            default:
                                logger.LogWarning($"Unsupported HTTP method '{methodAttr.Method}' detected for '{grainType.FullName}.{method.Name}' {routePattern?.RawText}. This route will be ignored.");
                                continue;
                        }

                        mapFunc = (p, d) => methodMapFunc(p.RawText, d);
                    }

                    if (routePattern == null)
                    {
                        logger.LogWarning($"Can not create a route pattern for '{grainType.FullName}.{method.Name}'. This route will be ignored.");
                        continue;
                    }

                    // mapFunc.Invoke(routePattern, requestDelegate);
                    dispatcher.RegisterRoute(routePattern, method);
                    mapFunc.Invoke(routePattern, context =>
                    {
                        return dispatcher.Dispatch(routePattern, context);
                    });
                    logger.LogInformation($"[{httpMethod}] [{grainType.FullName}.{method.Name}] -> {routePattern.RawText}.");
                    routesRegistered++;
                }
            }

            logger.LogInformation($"{routesRegistered} route(s) for grain '{grainType.FullName}' were created.");
            return routesRegistered;
        }

        private static List<Type> DiscoverGrainTypesToMap(GrainInterfaceFeature grainInterfaceFeature)
        {
            var grainTypesToMap = new List<Type>();

            foreach (var grainInterfaceMetadata in grainInterfaceFeature.Interfaces)
            {
                var grainType = grainInterfaceMetadata.InterfaceType;

                // Only add to the list grains that either have the top-level route attribute or has one of the method attributes
                if (grainType.GetCustomAttributes(false).Contains(_routeAttributeType) ||
                    grainType.GetMethods()
                        .Any(m => m.GetCustomAttributes(false)
                            .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))))
                {
                    grainTypesToMap.Add(grainType);
                }
            }

            return grainTypesToMap;
        }
    }
}