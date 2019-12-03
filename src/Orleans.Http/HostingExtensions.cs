using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orleans.ApplicationParts;
using Orleans.Metadata;
using System.Linq;
using Orleans.Http.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using ASPNetAuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

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
        private static readonly Type _authorizeAttributeType = typeof(AuthorizeAttribute);

        public static IServiceCollection AddGrainRouter(this IServiceCollection services)
        {
            return services
                .AddSingleton<MediaTypeManager>()
                .AddSingleton<GrainRouter>()
                .AddSingleton<RouteGrainProviderFactory>()
                .AddSingleton<IRouteGrainProviderPolicyBuilder>(s => s.GetRequiredService<RouteGrainProviderFactory>());
        }

        public static IServiceCollection AddJsonMediaType(this IServiceCollection services, Action<JsonSerializerOptions> configure = null)
        {
            var options = new JsonSerializerOptions();
            if (configure == null)
            {
                options.PropertyNameCaseInsensitive = true;
                options.AllowTrailingCommas = true;
            }
            else
            {
                configure.Invoke(options);
            }

            return services
                .AddSingleton<IMediaTypeHandler, JsonMediaTypeHandler>(sp => new JsonMediaTypeHandler(options));
        }

        public static IServiceCollection AddXmlMediaType(this IServiceCollection services)
        {
            return services
                .AddSingleton<IMediaTypeHandler, XMLMediaTypeHandler>();
        }

        public static IServiceCollection AddFormsMediaType(this IServiceCollection services)
        {
            return services
                .AddSingleton<IMediaTypeHandler, FormsMediaTypeHandler>();
        }

        public static IApplicationBuilder UseRouteGrainProviders(this IApplicationBuilder applicationBuilder, Action<IRouteGrainProviderPolicyBuilder> configureRouteGrainProviderPolicies)
        {
            var routeGrainProviderPolicyBuilder = applicationBuilder.ApplicationServices.GetRequiredService<IRouteGrainProviderPolicyBuilder>();

            configureRouteGrainProviderPolicies?.Invoke(routeGrainProviderPolicyBuilder);

            return applicationBuilder;
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

            var dispatcher = sp.GetRequiredService<GrainRouter>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<GrainRouter>();
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

        private static int MapGrainToRoute(IEndpointRouteBuilder routes, Type grainType, string prefix, GrainRouter dispatcher, ILogger logger)
        {
            logger.LogInformation($"Mapping routes for grain '{grainType.FullName}'...");

            // First we check if the grain has the [RouteAttribute] applied to the interface so we capture its info for as a prefix
            var topRouteAttr = (RouteAttribute)grainType.GetCustomAttributes(true).Where(attr => attr.GetType() == _routeAttributeType).SingleOrDefault();
            var topAuthorizeAttr = (AuthorizeAttribute)grainType.GetCustomAttributes(true).Where(attr => attr.GetType() == _authorizeAttributeType).SingleOrDefault();
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
            var methods = grainType.GetMethods().Where(m => m.GetCustomAttributes(true)
                            .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))).ToArray();

            int routesRegistered = 0;
            foreach (var method in methods)
            {
                var methodAttributes = method.GetCustomAttributes(true)
                    .Where(attr => attr.GetType() == _routeAttributeType ||
                        attr.GetType() == _authorizeAttributeType ||
                        _methodAttributeTypes.Contains(attr.GetType()));

                foreach (var attribute in methodAttributes)
                {
                    Func<RoutePattern, RequestDelegate, IEndpointConventionBuilder> mapFunc = default;
                    var httpMethod = string.Empty;
                    RoutePattern routePattern = default;
                    var routeGrainProviderPolicy = string.Empty;

                    if (attribute is RouteAttribute routeAttr)
                    {
                        routePattern = RoutePatternBuilder.BuildRoutePattern(prefix, topLevelPattern, grainType.FullName, method.Name, routeAttr.Pattern);
                        routeGrainProviderPolicy = routeAttr.RouteGrainProviderPolicy;
                        httpMethod = "*";
                        mapFunc = routes.Map;
                    }
                    else if (attribute is MethodAttribute methodAttr)
                    {
                        routePattern = RoutePatternBuilder.BuildRoutePattern(prefix, topLevelPattern, grainType.FullName, method.Name, methodAttr.Pattern);
                        routeGrainProviderPolicy = methodAttr.RouteGrainProviderPolicy;

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

                        httpMethod = methodAttr.Method;
                        mapFunc = (p, d) => methodMapFunc(p.RawText, d);
                    }

                    if (routePattern == null)
                    {
                        logger.LogWarning($"Can not create a route pattern for '{grainType.FullName}.{method.Name}'. This route will be ignored.");
                        continue;
                    }

                    if (!dispatcher.RegisterRoute(routePattern.RawText, httpMethod, method, routeGrainProviderPolicy))
                    {
                        throw new InvalidOperationException($"Dupplicated route pattern '{routePattern.RawText}'. A route with this pattern already exist.");
                    }

                    var route = mapFunc.Invoke(routePattern, dispatcher.Dispatch);

                    var methodAuthorizeAttr = (AuthorizeAttribute)methodAttributes.FirstOrDefault(attr => attr is AuthorizeAttribute);
                    AuthorizeAttribute authorizeAttr = default;

                    if (methodAuthorizeAttr != null)
                    {
                        authorizeAttr = methodAuthorizeAttr;
                    }
                    else if (topAuthorizeAttr != null)
                    {
                        authorizeAttr = methodAuthorizeAttr;
                    }

                    if (authorizeAttr != null)
                    {
                        route.RequireAuthorization(
                            new ASPNetAuthorizeAttribute(authorizeAttr.Policy)
                            {
                                Roles = authorizeAttr.Roles,
                                AuthenticationSchemes = authorizeAttr.AuthenticationSchemes
                            });
                    }

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
                if (grainType.GetCustomAttributes(true).Contains(_routeAttributeType) ||
                    grainType.GetMethods()
                        .Any(m => m.GetCustomAttributes(true)
                            .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))))
                {
                    grainTypesToMap.Add(grainType);
                }
            }

            return grainTypesToMap;
        }
    }
}