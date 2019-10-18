using System;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Orleans.Http.Abstractions;
using ProtoBuf;

namespace Orleans.Http.Test
{
    [Route("Test")]
    public interface ITestGrain : IGrainWithGuidKey
    {
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
    }

    [ProtoContract]
    public class TestPayload
    {
        [ProtoMember(1)]
        public int Number { get; set; }
        [ProtoMember(2)]
        public string Text { get; set; }
    }

    public class TestGrain : Grain, ITestGrain
    {
        public Task Get()
        {
            return Task.CompletedTask;
        }

        public Task<string> Get2() => Task.FromResult("Get2");

        public Task<string> Get3(string hello) => Task.FromResult(hello);

        public Task<string> Get4([FromQuery] string hello) => Task.FromResult(hello);

        public Task<Immutable<string>> Get5([FromQuery] Immutable<string> hello) => Task.FromResult(hello.Value.AsImmutable());

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
    }
}