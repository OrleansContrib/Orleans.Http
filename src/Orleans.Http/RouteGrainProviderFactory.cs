using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal sealed class RouteGrainProviderFactory : IRouteGrainProviderPolicyBuilder
    {
        private readonly IServiceProvider _serviceProvider;

        private Dictionary<string, Type> _routeGrainProviders = new Dictionary<string, Type>();
        private string _defaultPolicyName;

        public RouteGrainProviderFactory(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
        }

        public IRouteGrainProvider Create(string routeGrainProviderPolicy)
        {
            if(this._routeGrainProviders.TryGetValue(routeGrainProviderPolicy, out var routeGrainProviderType))
            {
                return (IRouteGrainProvider)ActivatorUtilities.GetServiceOrCreateInstance(this._serviceProvider, routeGrainProviderType);
            }

            throw new ArgumentException($"No RouteGrainProvider found for policy \"{routeGrainProviderPolicy}\"");
        }

        public IRouteGrainProvider CreateDefault()
        {
            if (string.IsNullOrEmpty(this._defaultPolicyName))
            {
                return (IRouteGrainProvider)ActivatorUtilities.GetServiceOrCreateInstance(this._serviceProvider, typeof(GrainIdFromRouteGrainProvider));
            }
            return this.Create(this._defaultPolicyName);
        }

        public IRouteGrainProviderPolicyBuilder RegisterRouteGrainProvider<T>(string policyName) where T : IRouteGrainProvider
        {
            this._routeGrainProviders[policyName] = typeof(T);
            return this;
        }

        public IRouteGrainProviderPolicyBuilder SetDefaultRouteGrainProviderPolicy(string policyName)
        {
            this._defaultPolicyName = policyName;
            return this;
        }
    }
}
