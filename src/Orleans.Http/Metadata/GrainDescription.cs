using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Orleans.Http.Metadata
{
    /// <summary>
    /// The grain description.
    /// </summary>
    public class GrainDescription
    {
        /// <summary>
        /// Gets or sets the kind.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this grain is a singleton.
        /// </summary>
        public bool IsSingleton { get; set; }

        /// <summary>
        /// Gets or sets the methods.
        /// </summary>
        public Dictionary<string, GrainMethodDescription> Methods { get; set; }

        /// <summary>
        /// Gets or sets the grain type.
        /// </summary>
        [JsonIgnore]
        public Type Type { get; set; }
    }
}