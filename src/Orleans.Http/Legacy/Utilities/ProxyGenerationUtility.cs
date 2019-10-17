using System;
using System.Reflection;
using Orleans.Http.Abstractions;

namespace Orleans.Http.Utilities
{
    /// <summary>
    /// Utilities for use during proxy generation.
    /// </summary>
    internal static class ProxyGenerationUtility
    {
        /// <summary>
        /// Returns a value indicating whether or not a server-side dispatcher should be generated for the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="containingType">
        /// The containing type.
        /// </param>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// A value indicating whether or not a server-side dispatcher should be generated for the provided <paramref name="method"/>.
        /// </returns>
        public static bool IsVisible(Type containingType, MethodInfo method)
        {
            var typeLevelAttribute = method.DeclaringType?.GetCustomAttribute<HttpVisibleAttribute>()
                                     ?? containingType.GetCustomAttribute<HttpVisibleAttribute>();
            var hasTypeOverride = typeLevelAttribute?.Visible != null;
            var typeVisibility = typeLevelAttribute?.Visible != null && typeLevelAttribute.Visible.Value;

            var methodLevelAttribute = method.GetCustomAttribute<HttpVisibleAttribute>();
            var hasMethodOverride = methodLevelAttribute?.Visible != null;
            var methodVisibility = methodLevelAttribute?.Visible != null && methodLevelAttribute.Visible.Value;

            if (hasMethodOverride)
            {
                return methodVisibility;
            }

            if (hasTypeOverride)
            {
                return typeVisibility;
            }

            return true;
        }

        /// <summary>
        /// Returns the canonical symbol name for the provided <paramref name="symbol"/>.
        /// </summary>
        /// <param name="symbol">
        /// The symbol name.
        /// </param>
        /// <returns>
        /// The canonical symbol name for the provided <paramref name="symbol"/>.
        /// </returns>
        public static string ToCanonicalName(string symbol)
        {
            return symbol.Substring(0, 1).ToLowerInvariant() + symbol.Substring(1);
        }
    }
}
