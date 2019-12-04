using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans.Concurrency;
using Orleans.Http.Abstractions;
using ProtoBuf;
using System.Collections.Generic;

namespace Orleans.Http.Test
{
    [Route("Test")]
    public interface ITestGrain : IGrainWithGuidKey
    {
        Task PrivateMethod();

        [HttpGet]
        Task Get();

        [HttpPost]
        Task Post();

        [HttpGet("{grainId}/Get2")]
        Task<string> Get2();

        [HttpPost("Post2")]
        Task Post2();

        [HttpGet("{grainId}/Get3/{hello}")]
        Task<string> Get3(string hello);

        [HttpPost("Post3")]
        Task Post3(TestPayload body);

        [HttpGet("Get4")]
        Task<string> Get4([FromQuery]string hello);

        [HttpPost]
        Task Post4([FromBody]TestPayload body);

        [HttpGet("Get5")]
        Task<Immutable<string>> Get5([FromQuery]Immutable<string> hello);

        [HttpPost("Post5")]
        Task Post5([FromBody]Immutable<TestPayload> body);

        [HttpPost("Post6")]
        Task Post6([FromQuery]Immutable<string> hello, [FromBody]Immutable<TestPayload> body);

        [HttpPost]
        Task<TestPayload> Post7([FromBody]TestPayload body);

        [Authorize]
        [HttpGet]
        Task<AuthResponse> GetWithAuth();

        [Authorize(Roles = "admin")]
        [HttpGet]
        Task<AuthResponse> GetWithAuthAdmin();

        [HttpGet("{grainId}/GetCustom")]
        Task<IGrainHttpResult<TestPayload>> GetCustomStatus();

        [HttpGet("{grainId}/SameUrl")]
        Task SameUrlGet();

        [HttpPost("{grainId}/SameUrl")]
        Task SameUrlPost();

        [HttpGet("{grainId}/SameUrlAndMethod")]
        [HttpPost("{grainId}/SameUrlAndMethod")]
        Task SameUrlAndMethod();

        [HttpGet(pattern: "Get6", routeGrainProviderPolicy: nameof(RandomGuidRouteGrainProvider))]
        Task Get6();

        [HttpGet(pattern: "Get7", routeGrainProviderPolicy: nameof(FailingRouteGrainProvider))]
        Task Get7();

        [HttpGet(pattern: "Get8")]
        Task Get8();
    }

    [ProtoContract]
    public class TestPayload
    {
        [ProtoMember(1)]
        public int Number { get; set; }
        [ProtoMember(2)]
        public string Text { get; set; }
    }

    [ProtoContract]
    public class AuthResponse
    {
        [ProtoMember(1)]
        public string User { get; set; }
        [ProtoMember(2)]
        public string Role { get; set; }
    }

    public class TestGrain : Grain, ITestGrain
    {
        private readonly IHttpContextAccessor _httpConntextAcessor;

        public TestGrain(IHttpContextAccessor acessor)
        {
            this._httpConntextAcessor = acessor;
        }

        public Task Get()
        {
            return Task.CompletedTask;
        }

        public Task<string> Get2() => Task.FromResult("Get2");

        public Task<string> Get3(string hello) => Task.FromResult(hello);

        public Task<string> Get4([FromQuery] string hello) => Task.FromResult(hello);

        public Task<Immutable<string>> Get5([FromQuery] Immutable<string> hello) => Task.FromResult(hello.Value.AsImmutable());

        public Task PrivateMethod() => throw new NotSupportedException();

        public Task Post() => Task.CompletedTask;

        public Task Post2() => Task.CompletedTask;

        public Task Post3(TestPayload body) => Task.FromResult(body);

        public Task Post4([FromBody] TestPayload body) => Task.FromResult(body);

        public Task Post5([FromBody] Immutable<TestPayload> body) => Task.FromResult(body.Value.AsImmutable());

        public Task Post6([FromQuery] Immutable<string> hello, [FromBody] Immutable<TestPayload> body)
        {
            if (string.IsNullOrWhiteSpace(hello.Value)) throw new ArgumentNullException(nameof(hello));
            if (body.Value == null) throw new ArgumentNullException(nameof(body));

            return Task.CompletedTask;
        }

        public Task<TestPayload> Post7([FromBody] TestPayload body) => Task.FromResult(body);

        public Task<AuthResponse> GetWithAuth()
        {
            return Task.FromResult(new AuthResponse
            {
                User = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Name).Value,
                Role = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Role).Value
            });
        }

        public Task<IGrainHttpResult<TestPayload>> GetCustomStatus()
        {
            return Task.FromResult(
                this.Created(
                    new TestPayload
                    {
                        Number = 1,
                        Text = "IGrainHttpResult"
                    },
                    new Dictionary<string, string> { { "CustomHeader", "HeaderValue" } }
                )
            );
        }

        public Task<AuthResponse> GetWithAuthAdmin()
        {
            return Task.FromResult(new AuthResponse
            {
                User = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Name).Value,
                Role = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Role).Value
            });
        }

        public Task SameUrlGet() => Task.CompletedTask;

        public Task SameUrlPost() => Task.CompletedTask;

        public Task SameUrlAndMethod() => Task.CompletedTask;

        public Task Get6() => Task.CompletedTask;

        public Task Get7() => Task.CompletedTask;

        public Task Get8() => Task.CompletedTask;
    }

    public class RandomGuidRouteGrainProvider : IRouteGrainProvider
    {
        private readonly IClusterClient _cluserClient;

        public RandomGuidRouteGrainProvider(IClusterClient clusterClient)
        {
            _cluserClient = clusterClient;
        }

        public ValueTask<IGrain> GetGrain(Type grainType)
        {
            return new ValueTask<IGrain>(_cluserClient.GetGrain(grainType, Guid.NewGuid()));
        }
    }

    public class FailingRouteGrainProvider : IRouteGrainProvider
    {
        public ValueTask<IGrain> GetGrain(Type grainType)
        {
            IGrain nullResult = null;
            return new ValueTask<IGrain>(nullResult);
        }
    }
}