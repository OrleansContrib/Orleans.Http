using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Http.Abstractions;

namespace OrleansHttp.Grains
{
    public class RandomGuidRouteGrainProvider : IRouteGrainProvider
    {
        private readonly IClusterClient _cluserClient;

        public RandomGuidRouteGrainProvider(IClusterClient clusterClient)
        {
            _cluserClient = clusterClient;
        }

        public Task<IGrain> GetGrain(Type grainType)
        {
            return Task.FromResult(_cluserClient.GetGrain(grainType, Guid.NewGuid()));
        }
    }
}
