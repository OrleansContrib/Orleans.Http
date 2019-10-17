using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Orleans.Http.Metadata
{
    /// <summary>
    /// The grain method description.
    /// </summary>
    public class GrainMethodDescription
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the return type.
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// Gets or sets the args.
        /// </summary>
        public List<GrainMethodArgumentDescription> Args { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this method is externally visible.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// Gets or sets the method metadata.
        /// </summary>
        [JsonIgnore]
        public MethodInfo MethodInfo { get; set; }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string representation of this instance.</returns>
        public override string ToString()
        {
            var returnType = string.IsNullOrWhiteSpace(this.ReturnType) ? "void" : this.ReturnType;
            var args = string.Join(", ", this.Args ?? Enumerable.Empty<GrainMethodArgumentDescription>());
            return $"{returnType} {this.Name}({args})";
        }
    }
}