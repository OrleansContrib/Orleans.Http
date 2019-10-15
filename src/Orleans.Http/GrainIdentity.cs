using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Orleans.Http.Utilities;

namespace Orleans.Http
{
    /// <summary>
    /// Describes an address in the system.
    /// </summary>
    [Serializable]
    [DataContract]
    [JsonConverter(typeof(AddressJsonConverter))]
    public struct GrainIdentity : IEquatable<GrainIdentity>
    {
        /// <summary>
        /// The address split.
        /// </summary>
        private static char[] AddressSplit => new [] { '-' };

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainIdentity"/> class.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="id">
        /// The id.
        /// </param>
        public GrainIdentity(string type, string id)
        {
            this.Type = type;
            this.Id = id;
        }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        [DataMember]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        [DataMember]
        public string Type { get; set; }

        /// <summary>
        /// Convert the provided <paramref name="address"/> into an <see cref="GrainIdentity"/> and return it.
        /// </summary>
        /// <param name="address">
        /// The address.
        /// </param>
        /// <returns>
        /// The <see cref="GrainIdentity"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The provided <paramref name="address"/> is in the incorrect format.
        /// </exception>
        public static GrainIdentity FromString(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(nameof(address));
            }

            var split = address.Split(AddressSplit, 2);
            if (split.Length != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "Address must be of the form \"{type}-{id}\", got: \"" + address + "\"");
            }

            return new GrainIdentity(split[0], split[1]);
        }

        /// <summary>
        /// Returns a value indicating whether or not the provided values are equal.
        /// </summary>
        /// <param name="left">
        /// The first value.
        /// </param>
        /// <param name="right">
        /// The second value.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="left"/> is equal to <paramref name="right"/>, <see langword="false"/> otherwise.
        /// </returns>
        public static bool Equals(GrainIdentity left, GrainIdentity right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase) && left.Id.Equals(right.Id);
        }

        /// <summary>
        /// Returns a value indicating whether or not the provided values are equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="left"/> is equal to <paramref name="right"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool operator ==(GrainIdentity left, GrainIdentity right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Returns a value indicating whether or not the provided values are equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="left"/> is equal to <paramref name="right"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool operator !=(GrainIdentity left, GrainIdentity right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Returns a value indicating whether or not the <paramref name="other"/> value is equal to this instance.
        /// </summary>
        /// <param name="other">
        /// The other value.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="other"/> is equal to this instance, <see langword="false"/>
        /// otherwise.
        /// </returns>
        public bool Equals(GrainIdentity other)
        {
            return Equals(this, other);
        }

        /// <summary>
        /// Returns a value indicating whether or not the <paramref name="obj"/> value is equal to this instance.
        /// </summary>
        /// <param name="obj">
        /// The other value.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is equal to this instance, <see langword="false"/>
        /// otherwise.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(null, obj) || ReferenceEquals(null, this))
            {
                return false;
            }

            return obj is GrainIdentity && this.Equals((GrainIdentity)obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// The hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.Type != null ? this.Type.ToLowerInvariant().GetHashCode() : 0) * 397) ^ this.Id.GetHashCode();
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return this.Type + '-' + this.Id;
        }
    }
}
