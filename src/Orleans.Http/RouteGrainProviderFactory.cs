using System;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal sealed class RouteGrainProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        private Type _defaultRouteGrainProvider = typeof(GrainIdFromRouteGrainProvider);

        public RouteGrainProviderFactory(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
        }

        public IRouteGrainProvider Create(Type routeGrainProviderType)
        {
            if (typeof(IRouteGrainProvider).IsAssignableFrom(routeGrainProviderType))
            {
                return (IRouteGrainProvider)ActivatorUtilities.GetServiceOrCreateInstance(this._serviceProvider, routeGrainProviderType);
            }

            throw new InvalidOperationException($"Can not use type {routeGrainProviderType} as RouteGrainProvider, it must implement Orleans.Http.Abstractions.IRouteGrainProvider");
        }

        public IRouteGrainProvider CreateDefault()
        {
            return (IRouteGrainProvider)ActivatorUtilities.GetServiceOrCreateInstance(this._serviceProvider, this._defaultRouteGrainProvider);
        }

        public void SetDefaultRouteGrainProvider(Type routeGrainProviderType)
        {
            if (typeof(IRouteGrainProvider).IsAssignableFrom(routeGrainProviderType))
            {
                this._defaultRouteGrainProvider = routeGrainProviderType;
            }
            else
            {
                throw new InvalidOperationException($"Can not use type {routeGrainProviderType} as RouteGrainProvider, it must implement Orleans.Http.Abstractions.IRouteGrainProvider");
            }
        }
    }
}
