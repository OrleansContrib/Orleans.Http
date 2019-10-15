using System;
using System.Globalization;

namespace Orleans.Http.Abstractions
{
    /// <summary>
    ///     The grain attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpGrainAttribute : Attribute
    {
        private static readonly string[] suffices = new[] { "grain", "actor", "entity" };

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGrainAttribute"/> class.
        /// </summary>
        public HttpGrainAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGrainAttribute"/> class.
        /// </summary>
        public HttpGrainAttribute(string typeName)
        {
            if (typeName != null)
            {
                this.TypeName = typeName.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGrainAttribute"/> class.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        public HttpGrainAttribute(Type type)
        {
            var typeName = type.Name.ToLowerInvariant();
            if (type.IsInterface)
            {
                if (typeName.StartsWith("i", true, CultureInfo.InvariantCulture))
                {
                    typeName = typeName.Substring(1);
                }
            }

            foreach (var suffix in suffices)
            {
                if (typeName.EndsWith(suffix, true, CultureInfo.InvariantCulture))
                {
                    this.TypeName = typeName.Substring(0, typeName.Length - suffix.Length);
                }
            }

            this.TypeName = this.TypeName ?? typeName;
        }

        /// <summary>
        /// Gets the grain type name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this grain is a singleton.
        /// </summary>
        public bool IsSingleton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this grain is an abstract base class and is not directly addressable.
        /// </summary>
        public bool IsAbstract { get; set; }
    }
}