using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Orleans.Http.Metadata;

namespace Orleans.Http.Host.Controllers
{
    [Route("api/complete")]
    [ApiController]
    public class CompletionController
    {
        private readonly GrainMetadataCollection metadata;

        public CompletionController(GrainMetadataCollection metadata)
        {
            this.metadata = metadata;
        }

        /// <summary>
        /// Returns a stream of tab-completion suggestions for each of the provided <see cref="PartialCommand"/>s.
        /// </summary>
        /// <param name="command">
        /// The stream of partially typed commands.
        /// </param>
        /// <returns>
        /// A stream of tab-completion suggestions for each of the provided <see cref="PartialCommand"/>s.
        /// </returns>
        [HttpPost("command")]
        public IEnumerable<string> CompleteCommand([FromBody]PartialCommand command)
        {
            if (command.Args != null && command.Args.Count != 0)
            {
                return Enumerable.Empty<string>();
            }

            // Method completion.
            if (!this.metadata.Grains.TryGetValue(command.Type, out var grain))
            {
                return Enumerable.Empty<string>();
            }

            var results = new List<string>();
            var method = command.Method ?? string.Empty;
            results.AddRange(grain.Methods.Where(_ => _.Value.Visible && _.Key.StartsWith(method, StringComparison.OrdinalIgnoreCase)).Select(_ => _.Key));

            return results;
        }

        /// <summary>
        /// Returns a stream of tab-completion suggestions for each of the provided grain kinds.
        /// </summary>
        /// <param name="type">
        /// The partially typed grain type.
        /// </param>
        /// <returns>
        /// A stream of tab-completion suggestions for each of the provided grain kinds.
        /// </returns>
        [HttpGet("type/{type?}")]
        public List<string> CompleteKind([FromRoute] string type = null)
        {
            var result = new List<string>();
            type = type ?? string.Empty;
            var grains = this.metadata.Grains;
            if (type.EndsWith("/"))
            {
                if (grains.TryGetValue(type.Substring(0, type.IndexOf('/')), out _))
                {
                    result.Add(type + Guid.Empty.ToString("N"));
                }
            }
            else
            {
                result.AddRange(grains.Keys.Where(_ => _.StartsWith(type, StringComparison.OrdinalIgnoreCase)).Select(_ => "to " + _ + "/"));
            }

            return result;
        }

        [Route("grains")]
        [HttpGet]
        public Dictionary<string, GrainDescription> GetGrains()
        {
            return this.metadata.Grains;
        }

        /// <summary>
        /// Describes a partially completed console command.
        /// </summary>
        public class PartialCommand
        {
            /// <summary>
            /// Gets or sets the type.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the command.
            /// </summary>
            [JsonProperty("cmd")]
            public string Method { get; set; }

            /// <summary>
            /// Gets or sets the args.
            /// </summary>
            public List<string> Args { get; set; }
        }
    }
}