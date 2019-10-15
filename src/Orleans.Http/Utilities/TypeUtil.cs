using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orleans.Http.Metadata;

namespace Orleans.Http.Utilities
{
    /// <summary>
    /// Utilities for working with types.
    /// </summary>
    internal static class TypeUtil
    {
        /// <summary>
        /// Returns all namespaces required by <paramref name="grains"/>.
        /// </summary>
        /// <param name="grains">
        /// The grains.
        /// </param>
        /// <param name="additionalTypes">
        /// The additional types to include.
        /// </param>
        /// <returns>
        /// All namespaces required by <paramref name="grains"/>.
        /// </returns>
        public static IEnumerable<NameSyntax> GetNamespaces(
            IEnumerable<GrainDescription> grains, 
            params Type[] additionalTypes)
        {
            var namespaces =
                grains.SelectMany(GetTypes)
                    .Concat(additionalTypes.SelectMany(GetTypes))
                    .Select(type => type.Namespace)
                    .Distinct();

            return namespaces.Select(ns => SyntaxFactory.ParseName(ns));
        }

        /// <summary>
        /// Returns the types referenced by the provided <paramref name="grain"/>.
        /// </summary>
        /// <param name="grain">
        /// The grain.
        /// </param>
        /// <returns>
        /// The types referenced by the provided <paramref name="grain"/>.
        /// </returns>
        public static IEnumerable<Type> GetTypes(GrainDescription grain)
        {
            foreach (var type in GetTypes(grain.Type))
            {
                yield return type;
            }

            foreach (var type in grain.Methods.Values.SelectMany(GetTypes))
            {
                yield return type;
            }
        }

        /// <summary>
        /// Returns the types referenced by the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The types referenced by the provided <paramref name="method"/>.
        /// </returns>
        public static IEnumerable<Type> GetTypes(GrainMethodDescription method)
        {
            foreach (var type in GetTypes(method.MethodInfo.ReturnType))
            {
                yield return type;
            }

            var parameterTypes = method.MethodInfo.GetParameters().SelectMany(parameter => GetTypes(parameter.ParameterType));
            foreach (var parameterType in parameterTypes)
            {
                yield return parameterType;
            }
        }

        /// <summary>
        /// Returns the types referenced by the provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The types referenced by the provided <paramref name="type"/>.
        /// </returns>
        public static IEnumerable<Type> GetTypes(Type type)
        {
            yield return type;

            if (!type.IsGenericType) yield break;

            foreach (var generic in type.GetGenericArguments().SelectMany(GetTypes))
            {
                yield return generic;
            }
        }
    }
}
