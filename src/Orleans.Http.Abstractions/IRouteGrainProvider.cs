using System;
using System.Threading.Tasks;

namespace Orleans.Http.Abstractions
{
    public interface IRouteGrainProvider
    {
        ValueTask<IGrain> GetGrain(Type grainType);
    }
}
