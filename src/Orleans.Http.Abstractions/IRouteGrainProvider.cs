using System;
using System.Threading.Tasks;

namespace Orleans.Http.Abstractions
{
    public interface IRouteGrainProvider
    {
        Task<IGrain> GetGrain(Type grainType);
    }
}
