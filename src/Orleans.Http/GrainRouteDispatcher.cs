using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

namespace Orleans.Http
{
    internal class GrainRouteDispatcher
    {
        private const string GRAIN_ID = "grainId";
        private const string GRAIN_ID_EXTENSION = "grainIdExtension";
        private readonly IClusterClient _clusterClient;
        private readonly ILogger _logger;
        private readonly Dictionary<RoutePattern, (GrainIdType IdType, MethodInfo Method)> _routes = new Dictionary<RoutePattern, (GrainIdType, MethodInfo)>();

        public GrainRouteDispatcher(IClusterClient clusterClient, ILoggerFactory loggerFactory)
        {
            this._clusterClient = clusterClient;
            this._logger = loggerFactory.CreateLogger<GrainRouteDispatcher>();
        }

        public void RegisterRoute(RoutePattern pattern, MethodInfo method)
        {
            var grainInterfaceType = method.DeclaringType;
            var grainIdType = this.GetGrainIdType(grainInterfaceType);
            this._routes[pattern] = (grainIdType, method);
        }

        public async Task Dispatch(RoutePattern pattern, HttpContext context)
        {
            var match = this._routes[pattern];
            var method = match.Method;
            var grainIdType = match.IdType;
            var grainType = method.DeclaringType;

            IGrain grain = this.GetGrain(pattern, grainType, grainIdType, context);
            if (grain == null)
            {
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return;
            }

            // TODO: Implement proper parameter binding
            // TODO: Receive result and write to the HttpContext.Response
            await ((Task)method.Invoke(grain, null));

            return;
        }

        private IGrain GetGrain(RoutePattern pattern, Type grainType, GrainIdType grainIdType, HttpContext context)
        {
            try
            {
                var grainIdParameter = context.Request.RouteValues[GRAIN_ID];
                var grainIdExtensionParameter = context.Request.RouteValues.ContainsKey(GRAIN_ID_EXTENSION) ? context.Request.RouteValues[GRAIN_ID] : null;
                switch (grainIdType)
                {
                    case GrainIdType.String:
                        string stringId = (string)grainIdParameter;
                        return this._clusterClient.GetGrain(grainType, stringId);
                    case GrainIdType.Integer:
                        long integerId = Convert.ToInt64(grainIdParameter);
                        return this._clusterClient.GetGrain(grainType, integerId);
                    case GrainIdType.IntegerCompound:
                        return this._clusterClient.GetGrain(grainType, Convert.ToInt64(grainIdParameter), (string)grainIdExtensionParameter);
                    case GrainIdType.GuidCompound:
                        return this._clusterClient.GetGrain(grainType, Guid.Parse((string)grainIdParameter), (string)grainIdExtensionParameter);
                    default:
                        return this._clusterClient.GetGrain(grainType, Guid.Parse((string)grainIdParameter));
                }
            }
            catch (Exception exc)
            {
                this._logger.LogError(exc, $"Failure getting grain '{grainType.FullName} | {grainIdType}' for route '{pattern.RawText}': {exc.Message}");
                return null;
            }
        }

        private GrainIdType GetGrainIdType(Type grainInterfaceType)
        {
            var ifaces = grainInterfaceType.GetInterfaces();
            if (ifaces.Contains(typeof(IGrainWithGuidKey)))
            {
                return GrainIdType.Guid;
            }
            else if (ifaces.Contains(typeof(IGrainWithGuidCompoundKey)))
            {
                return GrainIdType.GuidCompound;
            }
            else if (ifaces.Contains(typeof(IGrainWithIntegerKey)))
            {
                return GrainIdType.Integer;
            }
            else if (ifaces.Contains(typeof(IGrainWithIntegerCompoundKey)))
            {
                return GrainIdType.IntegerCompound;
            }
            else
            {
                return GrainIdType.String;
            }
        }
    }

    internal enum GrainIdType
    {
        Guid = 0,
        String = 1,
        Integer = 2,
        GuidCompound = 3,
        IntegerCompound = 4
    }
}