namespace Orleans.Http.Metadata
{
    /// <summary>
    /// The grain method argument description.
    /// </summary>
    public class GrainMethodArgumentDescription
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string representation of this instance.</returns>
        public override string ToString()
        {
            return $"{this.Type} {this.Name}";
        }
    }
}