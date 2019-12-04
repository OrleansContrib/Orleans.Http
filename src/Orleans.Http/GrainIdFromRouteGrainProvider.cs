using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    public class GrainIdFromRouteGrainProvider : IRouteGrainProvider
    {
        private enum GrainIdType
        {
            Guid = 0,
            String = 1,
            Integer = 2,
            GuidCompound = 3,
            IntegerCompound = 4
        }

        private static readonly ValueTask<IGrain> _nullGrainReference = default;

        private readonly IClusterClient _clusterClient;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GrainIdFromRouteGrainProvider(IClusterClient clusterClient, ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
        {
            _clusterClient = clusterClient;
            _logger = loggerFactory.CreateLogger<GrainIdFromRouteGrainProvider>();
            _httpContextAccessor = httpContextAccessor;
        }

        public ValueTask<IGrain> GetGrain(Type grainType)
        {
            var context = this._httpContextAccessor.HttpContext;
            var endpoint = (RouteEndpoint)context.GetEndpoint();
            var pattern = endpoint.RoutePattern;

            try
            {
                if (context.Request.RouteValues.ContainsKey(Constants.GRAIN_ID))
                {
                    var grainIdType = this.GetGrainIdType(grainType);
                    var grainIdParameter = context.Request.RouteValues[Constants.GRAIN_ID];
                    var grainIdExtensionParameter = context.Request.RouteValues.ContainsKey(Constants.GRAIN_ID_EXTENSION) ? context.Request.RouteValues[Constants.GRAIN_ID] : null;
                    switch (grainIdType)
                    {
                        case GrainIdType.String:
                            string stringId = (string)grainIdParameter;
                            return new ValueTask<IGrain>(this._clusterClient.GetGrain(grainType, stringId));
                        case GrainIdType.Integer:
                            long integerId = Convert.ToInt64(grainIdParameter);
                            return new ValueTask<IGrain>(this._clusterClient.GetGrain(grainType, integerId));
                        case GrainIdType.IntegerCompound:
                            return new ValueTask<IGrain>(this._clusterClient.GetGrain(grainType, Convert.ToInt64(grainIdParameter), (string)grainIdExtensionParameter));
                        case GrainIdType.GuidCompound:
                            return new ValueTask<IGrain>(this._clusterClient.GetGrain(grainType, Guid.Parse((string)grainIdParameter), (string)grainIdExtensionParameter));
                        default:
                            return new ValueTask<IGrain>(this._clusterClient.GetGrain(grainType, Guid.Parse((string)grainIdParameter)));
                    }
                }

                context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return _nullGrainReference;
            }
            catch (Exception exc)
            {
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                this._logger.LogError(exc, $"Failure getting grain '{grainType.FullName}' for route '{pattern.RawText}': {exc.Message}");
                return _nullGrainReference;
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
}
