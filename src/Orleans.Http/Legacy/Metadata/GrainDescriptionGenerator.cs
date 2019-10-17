using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Orleans.Http.Abstractions;
using Orleans.Http.Utilities;

namespace Orleans.Http.Metadata
{
    /// <summary>
    /// The grain description generator.
    /// </summary>
    public static class GrainDescriptionGenerator
    {
        /// <summary>
        /// The cache of grain descriptions.
        /// </summary>
        private static readonly ConcurrentDictionary<Assembly, Dictionary<string, GrainDescription>> GrainDescriptions =
            new ConcurrentDictionary<Assembly, Dictionary<string, GrainDescription>>();

        /// <summary>
        /// Returns all grain descriptions for the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly.
        /// </param>
        /// <returns>
        /// All grain descriptions for the specified assembly.
        /// </returns>
        public static Dictionary<string, GrainDescription> GetGrainDescriptions(Assembly assembly)
        {
            return GrainDescriptions.GetOrAdd(
                assembly, 
                asm =>
                asm.GetTypes()
                    .Where(ShouldGenerateGrainDescription)
                    .Select(GetGrainDescription)
                    .ToDictionary(_ => _.Kind, _ => _));
        }

        /// <summary>
        /// Returns all grain descriptions for the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The grain assemblies.
        /// </param>
        /// <returns>
        /// All grain descriptions for the specified assembly.
        /// </returns>
        public static Dictionary<string, GrainDescription> GetGrainDescriptions(IEnumerable<Assembly> assemblies)
        {
            return assemblies.SelectMany(GetGrainDescriptions).ToDictionary(_ => _.Key, _ => _.Value);
        }

        /// <summary>
        /// Returns a value indicating whether or not an grain description should be generated for the provided type.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// A value indicating whether or not an grain description should be generated for the provided type.
        /// </returns>
        private static bool ShouldGenerateGrainDescription(Type type)
        {
            // If the type is concrete, a description should not be generated.
            if (!type.IsInterface)
            {
                return false;
            }

            // If the interface has an Grain attribute, a description should be generated only if it is not marked as
            // abstract.
            var attr = type.GetCustomAttribute<HttpGrainAttribute>();
            if (attr != null)
            {
                return !attr.IsAbstract;
            }

            // By default, all grain interfaces should have a description generated.
            return typeof(IGrainWithStringKey).IsAssignableFrom(type) && typeof(IGrainWithStringKey) != type
                   && type.IsPublic;
        }

        /// <summary>
        /// Returns the description of the provided grain type.
        /// </summary>
        /// <param name="type">
        /// The grain type.
        /// </param>
        /// <returns>
        /// The description of the provided grain type.
        /// </returns>
        public static GrainDescription GetGrainDescription(Type type)
        {
            var grainAttr = type.GetCustomAttribute<HttpGrainAttribute>() ?? new HttpGrainAttribute(type);
            var result = new GrainDescription
                         {
                             Kind = grainAttr.TypeName, 
                             IsSingleton = grainAttr.IsSingleton, 
                             Methods = new Dictionary<string, GrainMethodDescription>(), 
                             Type = type
                         };

            // Get all server-visible methods from the type.
            var methods = type.GetInterfaces().SelectMany(iface => iface.GetMethods()).Concat(type.GetMethods());
            foreach (var method in methods)
            {
                var methodDescription = GetMethodDescription(method);
                var ev = method.GetCustomAttribute<HttpGrainMethodAttribute>();
                var name = ev != null ? ev.Name : ProxyGenerationUtility.ToCanonicalName(method.Name);

                result.Methods[name] = methodDescription;
            }

            return result;
        }

        /// <summary>
        /// Returns the description of the provided grain method.
        /// </summary>
        /// <param name="method">
        /// The grain method.
        /// </param>
        /// <returns>
        /// The description of the provided grain method.
        /// </returns>
        private static GrainMethodDescription GetMethodDescription(MethodInfo method)
        {
            var methodParameters = method.GetParameters().ToList();
            string returnTypeName;
            if (method.ReturnType == typeof(Task))
            {
                returnTypeName = string.Empty;
            }
            else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                returnTypeName = GetDisplayTypeName(method.ReturnType.GenericTypeArguments[0]);
            }
            else
            {
                returnTypeName = GetDisplayTypeName(method.ReturnType);
            }

            return new GrainMethodDescription
            {
                ReturnType = returnTypeName, 
                Name = GetMethodName(method), 
                Args = new List<GrainMethodArgumentDescription>(methodParameters.Select(GetParameterDescription)), 
                MethodInfo = method, 
                Visible = ProxyGenerationUtility.IsVisible(method.DeclaringType, method)
            };
        }

        /// <summary>
        /// Returns a string representing the provided <paramref name="type"/>, suitable for display.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// A string representing the provided <paramref name="type"/>, suitable for display.
        /// </returns>
        private static string GetDisplayTypeName(Type type)
        {
            string name;
            if (type.IsGenericType)
            {
                var typeName = type.Name.Substring(0, type.Name.IndexOf('`'));
                var args = string.Join(", ", type.GenericTypeArguments.Select(GetDisplayTypeName));
                name = $"{typeName}<{args}>";
            }
            else
            {
                name = type.Name;
            }

            return ProxyGenerationUtility.ToCanonicalName(name);
        }

        /// <summary>
        /// Returns the description of the provided parameter.
        /// </summary>
        /// <param name="parameter">
        /// The parameter.
        /// </param>
        /// <returns>
        /// The description of the provided parameter.
        /// </returns>
        private static GrainMethodArgumentDescription GetParameterDescription(ParameterInfo parameter)
        {
            return new GrainMethodArgumentDescription
            {
                Name = ProxyGenerationUtility.ToCanonicalName(parameter.Name), 
                Type = GetDisplayTypeName(parameter.ParameterType)
            };
        }

        /// <summary>
        /// Returns the event name for the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The event name for the provided <paramref name="method"/>.
        /// </returns>
        private static string GetMethodName(MemberInfo method)
        {
            var ev = method.GetCustomAttribute<HttpGrainMethodAttribute>();
            return ev != null ? ev.Name : ProxyGenerationUtility.ToCanonicalName(method.Name);
        }
    }
}