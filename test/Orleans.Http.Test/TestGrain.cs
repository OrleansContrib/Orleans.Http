using System;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Orleans.Http.Abstractions;

namespace Orleans.Http.Test
{
    [Route("Test")]
    public interface ITestGrain : IGrainWithGuidKey
    {
        [HttpGet]
        Task Get();

        [HttpPost]
        Task Post();

        [HttpGet("Get2")]
        Task<string> Get2();

        [HttpPost("Post2")]
        Task Post2();

        [HttpGet("Get3")]
        Task<string> Get3(string hello);

        [HttpPost("Post3")]
        Task Post3(object body);

        [HttpGet("Get4")]
        Task<string> Get4([FromQuery]string hello);

        [HttpPost("Post4")]
        Task Post4([FromBody]object body);

        [HttpGet("Get5")]
        Task<Immutable<string>> Get5([FromQuery]Immutable<string> hello);

        [HttpPost("Post5")]
        Task Post5([FromBody]Immutable<object> body);

        [HttpPost("Post6")]
        Task Post6([FromQuery]Immutable<string> hello, [FromBody]Immutable<object> body);
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

        public Task Post3(object body) => Task.FromResult(body);

        public Task Post4([FromBody] object body) => Task.FromResult(body);

        public Task Post5([FromBody] Immutable<object> body) => Task.FromResult(body.Value.AsImmutable());

        public Task Post6([FromQuery] Immutable<string> hello, [FromBody] Immutable<object> body)
        {
            if (string.IsNullOrWhiteSpace(hello.Value)) throw new ArgumentNullException(nameof(hello));
            if (body.Value == null) throw new ArgumentNullException(nameof(body));

            return Task.CompletedTask;
        }
    }
}