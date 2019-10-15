using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Orleans.Http.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController
    {
        private readonly GrainMetadataCollection metadata;

        public DiagnosticsController(GrainMetadataCollection metadata)
        {
            this.metadata = metadata;
        }

        [HttpGet("dispatchers")]
        public Task<string> GetDispatchers()
        {
            return Task.FromResult(this.metadata.DispatcherSource);
        }
    }
}