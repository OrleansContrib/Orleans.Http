using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.Http.Abstractions;
using Orleans.Runtime;

namespace OrleansHttp.Grains
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        // GET grains/OrleansHttp.Grains.IHelloGrain/{grainId}/Hello?name={name}
        [HttpGet]
        Task<string> Hello([FromQuery]string name);

        // GET grains/Hello?name={name}
        [HttpGet(pattern: "Hello", routeGrainProviderPolicy: nameof(RandomGuidRouteGrainProvider))]
        Task<string> SimpleHello([FromQuery]string name);

        // GET grains/{grainId}/token?admin=[true/false]
        [HttpGet("{grainId}/token")]
        Task<string> GetToken([FromQuery]bool admin);

        // GET grains/{grainId}/silos
        [Authorize(Roles = "admin")]
        [HttpGet("{grainId}/silos")]
        Task<SiloInfo[]> GetSilos();

        // GET grains/{grainId}/user
        [Authorize]
        [HttpGet("{grainId}/user")]
        Task<UserInfo> GetUserInfo();
    }

    public class SiloInfo
    {
        public string Silo { get; set; }
        public SiloStatus Status { get; set; }
    }

    public class UserInfo
    {
        public string User { get; set; }
        public string Roles { get; set; }
    }
}
