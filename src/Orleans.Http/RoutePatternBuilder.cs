using Microsoft.AspNetCore.Routing.Patterns;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal static class RoutePatternBuilder
    {
        private const string GRAIN_ID_TOKEN = "{grainId}";

        public static RoutePattern BuildRoutePattern(
            string prefix,
            string topLevelPattern,
            string grainTypeName,
            string methodName,
            string routeAttributePattern)
        {
            // If the user defined a pattern then lets use it
            if (!string.IsNullOrWhiteSpace(routeAttributePattern))
            {
                if (!ValidatePattern(routeAttributePattern)) return null;

                if (routeAttributePattern.StartsWith("/"))
                {
                    return RoutePatternFactory.Parse(routeAttributePattern);
                }
                else
                {
                    return RoutePatternFactory.Parse($"{prefix}{topLevelPattern}{routeAttributePattern}");
                }
            }

            // Otherwise lets use the default one
            return RoutePatternFactory.Parse($"{prefix}{topLevelPattern}{grainTypeName}/{{grainId}}/{methodName}");
        }

        private static bool ValidatePattern(string pattern)
        {
            // When more elaborated routes are supported, we will have more rules here
            return pattern.Contains(GRAIN_ID_TOKEN);
        }
    }
}