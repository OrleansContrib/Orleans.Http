using Microsoft.AspNetCore.Routing.Patterns;

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
    }
}