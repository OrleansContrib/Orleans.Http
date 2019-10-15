using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orleans.Http.Utilities
{
    /// <summary>
    /// The syntax factory extensions.
    /// </summary>
    internal static class SyntaxFactoryExtensions
    {
        private static readonly ConcurrentDictionary<Tuple<Type, TypeFormattingOptions>, string> ParseableNameCache = new ConcurrentDictionary<Tuple<Type, TypeFormattingOptions>, string>();

        public class TypeFormattingOptions : IEquatable<TypeFormattingOptions>
        {
            /// <summary>Initializes a new instance of <see cref="TypeFormattingOptions"/>.</summary>
            public TypeFormattingOptions(
                string nameSuffix = null,
                bool includeNamespace = true,
                bool includeGenericParameters = true,
                bool includeTypeParameters = true,
                char nestedClassSeparator = '.',
                bool includeGlobal = true)
            {

                this.NameSuffix = nameSuffix;
                this.IncludeNamespace = includeNamespace;
                this.IncludeGenericTypeParameters = includeGenericParameters;
                this.IncludeTypeParameters = includeTypeParameters;
                this.NestedTypeSeparator = nestedClassSeparator;
                this.IncludeGlobal = includeGlobal;
            }

            internal static TypeFormattingOptions Default { get; } = new TypeFormattingOptions();

            /// <summary>
            /// Gets a value indicating whether or not to include the fully-qualified namespace of the class in the result.
            /// </summary>
            public bool IncludeNamespace { get; }

            /// <summary>
            /// Gets a value indicating whether or not to include concrete type parameters in the result.
            /// </summary>
            public bool IncludeTypeParameters { get; }

            /// <summary>
            /// Gets a value indicating whether or not to include generic type parameters in the result.
            /// </summary>
            public bool IncludeGenericTypeParameters { get; }

            /// <summary>
            /// Gets the separator used between declaring types and their declared types.
            /// </summary>
            public char NestedTypeSeparator { get; }

            /// <summary>
            /// Gets the name to append to the formatted name, before any type parameters.
            /// </summary>
            public string NameSuffix { get; }

            /// <summary>
            /// Gets a value indicating whether or not to include the global namespace qualifier.
            /// </summary>
            public bool IncludeGlobal { get; }

            /// <summary>
            /// Indicates whether the current object is equal to another object of the same type.
            /// </summary>
            /// <param name="other">An object to compare with this object.</param>
            /// <returns>
            /// <see langword="true"/> if the specified object  is equal to the current object; otherwise, <see langword="false"/>.
            /// </returns>
            public bool Equals(TypeFormattingOptions other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return this.IncludeNamespace == other.IncludeNamespace
                       && this.IncludeTypeParameters == other.IncludeTypeParameters
                       && this.IncludeGenericTypeParameters == other.IncludeGenericTypeParameters
                       && this.NestedTypeSeparator == other.NestedTypeSeparator
                       && string.Equals(this.NameSuffix, other.NameSuffix) && this.IncludeGlobal == other.IncludeGlobal;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != this.GetType())
                {
                    return false;
                }

                return Equals((TypeFormattingOptions) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = this.IncludeNamespace.GetHashCode();
                    hashCode = (hashCode * 397) ^ this.IncludeTypeParameters.GetHashCode();
                    hashCode = (hashCode * 397) ^ this.IncludeGenericTypeParameters.GetHashCode();
                    hashCode = (hashCode * 397) ^ this.NestedTypeSeparator.GetHashCode();
                    hashCode = (hashCode * 397) ^ (this.NameSuffix != null ? this.NameSuffix.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ this.IncludeGlobal.GetHashCode();
                    return hashCode;
                }
            }

            /// <summary>Determines whether the specified objects are equal.</summary>
            public static bool operator ==(TypeFormattingOptions left, TypeFormattingOptions right)
            {
                return Equals(left, right);
            }

            /// <summary>Determines whether the specified objects are not equal.</summary>
            public static bool operator !=(TypeFormattingOptions left, TypeFormattingOptions right)
            {
                return !Equals(left, right);
            }
        }

        public static string GetUnadornedTypeName(this Type type)
        {
            var index = type.Name.IndexOf('`');

            // An ampersand can appear as a suffix to a by-ref type.
            return (index > 0 ? type.Name.Substring(0, index) : type.Name).TrimEnd('&');
        }

        public static string GetUnadornedMethodName(this MethodInfo method)
        {
            var index = method.Name.IndexOf('`');

            return index > 0 ? method.Name.Substring(0, index) : method.Name;
        }

        public static string GetParseableName(this Type type, TypeFormattingOptions options = null, Func<Type, string> getNameFunc = null)
        {
            options = options ?? TypeFormattingOptions.Default;

            // If a naming function has been specified, skip the cache.
            if (getNameFunc != null) return BuildParseableName();

            return ParseableNameCache.GetOrAdd(Tuple.Create(type, options), _ => BuildParseableName());

            string BuildParseableName()
            {
                var builder = new StringBuilder();
                var typeInfo = type.GetTypeInfo();
                GetParseableName(
                    type,
                    builder,
                    new Queue<Type>(
                        typeInfo.IsGenericTypeDefinition
                            ? typeInfo.GetGenericArguments()
                            : typeInfo.GenericTypeArguments),
                    options,
                    getNameFunc ?? (t => t.GetUnadornedTypeName() + options.NameSuffix));
                return builder.ToString();
            }
        }

        private static void GetParseableName(
            Type type,
            StringBuilder builder,
            Queue<Type> typeArguments,
            TypeFormattingOptions options,
            Func<Type, string> getNameFunc)
        {
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsArray)
            {
                var elementType = typeInfo.GetElementType().GetParseableName(options);
                if (!string.IsNullOrWhiteSpace(elementType))
                {
                    builder.AppendFormat(
                        "{0}[{1}]",
                        elementType,
                        string.Concat(Enumerable.Range(0, type.GetArrayRank() - 1).Select(_ => ',')));
                }

                return;
            }

            if (typeInfo.IsGenericParameter)
            {
                if (options.IncludeGenericTypeParameters)
                {
                    builder.Append(type.GetUnadornedTypeName());
                }

                return;
            }

            if (typeInfo.DeclaringType != null)
            {
                // This is not the root type.
                GetParseableName(typeInfo.DeclaringType, builder, typeArguments, options, t => t.GetUnadornedTypeName());
                builder.Append(options.NestedTypeSeparator);
            }
            else if (!string.IsNullOrWhiteSpace(type.Namespace) && options.IncludeNamespace)
            {
                // This is the root type, so include the namespace.
                var namespaceName = type.Namespace;
                if (options.NestedTypeSeparator != '.')
                {
                    namespaceName = namespaceName.Replace('.', options.NestedTypeSeparator);
                }

                if (options.IncludeGlobal)
                {
                    builder.AppendFormat("global::");
                }

                builder.AppendFormat("{0}{1}", namespaceName, options.NestedTypeSeparator);
            }

            if (type.IsConstructedGenericType)
            {
                // Get the unadorned name, the generic parameters, and add them together.
                var unadornedTypeName = getNameFunc(type);
                builder.Append(EscapeIdentifier(unadornedTypeName));
                var generics =
                    Enumerable.Range(0, Math.Min(typeInfo.GetGenericArguments().Count(), typeArguments.Count))
                        .Select(_ => typeArguments.Dequeue())
                        .ToList();
                if (generics.Count > 0 && options.IncludeTypeParameters)
                {
                    var genericParameters = string.Join(
                        ",",
                        generics.Select(generic => GetParseableName(generic, options)));
                    builder.AppendFormat("<{0}>", genericParameters);
                }
            }
            else if (typeInfo.IsGenericTypeDefinition)
            {
                // Get the unadorned name, the generic parameters, and add them together.
                var unadornedTypeName = getNameFunc(type);
                builder.Append(EscapeIdentifier(unadornedTypeName));
                var generics =
                    Enumerable.Range(0, Math.Min(type.GetGenericArguments().Count(), typeArguments.Count))
                        .Select(_ => typeArguments.Dequeue())
                        .ToList();
                if (generics.Count > 0 && options.IncludeTypeParameters)
                {
                    var genericParameters = string.Join(
                        ",",
                        generics.Select(_ => options.IncludeGenericTypeParameters ? _.ToString() : string.Empty));
                    builder.AppendFormat("<{0}>", genericParameters);
                }
            }
            else
            {
                builder.Append(EscapeIdentifier(getNameFunc(type)));
            }
        }

        public static IEnumerable<string> GetNamespaces(params Type[] types)
        {
            return types.Select(type => "global::" + type.Namespace).Distinct();
        }

        public static TypeSyntax GetTypeSyntax(
            this Type type)
        {
            if (type == typeof(void))
            {
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
            }

            return
                SyntaxFactory.ParseTypeName(type.GetParseableName(TypeFormattingOptions.Default));
        }

        public static NameSyntax GetNameSyntax(this Type type, bool includeNamespace = true)
        {
            return
                SyntaxFactory.ParseName(
                    type.GetParseableName(new TypeFormattingOptions(includeNamespace: includeNamespace)));
        }

        public static SimpleNameSyntax GetNameSyntax(this MethodInfo method)
        {
            var plainName = method.GetUnadornedMethodName();
            if (method.IsGenericMethod)
            {
                var args = method.GetGenericArguments().Select(arg => arg.GetTypeSyntax());
                return plainName.ToGenericName().AddTypeArgumentListArguments(args.ToArray());
            }

            return plainName.ToIdentifierName();
        }

        /// <summary>
        /// Returns <see cref="ArrayTypeSyntax"/> representing the array form of <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="includeNamespace">
        /// A value indicating whether or not to include the namespace name.
        /// </param>
        /// <returns>
        /// <see cref="ArrayTypeSyntax"/> representing the array form of <paramref name="type"/>.
        /// </returns>
        public static ArrayTypeSyntax GetArrayTypeSyntax(this Type type, bool includeNamespace = true)
        {
            return
                SyntaxFactory.ArrayType(
                    SyntaxFactory.ParseTypeName(
                        type.GetParseableName(new TypeFormattingOptions(includeNamespace: includeNamespace))))
                    .AddRankSpecifiers(
                        SyntaxFactory.ArrayRankSpecifier().AddSizes(SyntaxFactory.OmittedArraySizeExpression()));
        }

        /// <summary>
        /// Returns the method declaration syntax for the provided method.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The method declaration syntax for the provided method.
        /// </returns>
        public static MethodDeclarationSyntax GetDeclarationSyntax(this MethodInfo method)
        {
            var syntax =
                SyntaxFactory.MethodDeclaration(method.ReturnType.GetTypeSyntax(), method.Name.ToIdentifier())
                    .WithParameterList(SyntaxFactory.ParameterList().AddParameters(method.GetParameterListSyntax()));
            if (method.IsGenericMethodDefinition)
            {
                syntax = syntax.WithTypeParameterList(SyntaxFactory.TypeParameterList().AddParameters(method.GetTypeParameterListSyntax()));

                // Handle type constraints on type parameters.
                var typeParameters = method.GetGenericArguments();
                var typeParameterConstraints = new List<TypeParameterConstraintClauseSyntax>();
                foreach (var arg in typeParameters)
                {
                    typeParameterConstraints.AddRange(GetTypeParameterConstraints(arg));
                }

                if (typeParameterConstraints.Count > 0)
                {
                    syntax = syntax.AddConstraintClauses(typeParameterConstraints.ToArray());
                }
            }

            if (method.IsPublic)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            }
            else if (method.IsPrivate)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            }
            else if (method.IsFamily)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword));
            }

            return syntax;
        }

        /// <summary>
        /// Returns the method declaration syntax for the provided constructor.
        /// </summary>
        /// <param name="constructor">
        /// The constructor.
        /// </param>
        /// <param name="typeName">
        /// The name of the type which the constructor will reside on.
        /// </param>
        /// <returns>
        /// The method declaration syntax for the provided constructor.
        /// </returns>
        public static ConstructorDeclarationSyntax GetDeclarationSyntax(this ConstructorInfo constructor, string typeName)
        {
            var syntax =
                SyntaxFactory.ConstructorDeclaration(typeName.ToIdentifier())
                    .WithParameterList(SyntaxFactory.ParameterList().AddParameters(constructor.GetParameterListSyntax()));
            if (constructor.IsPublic)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            }
            else if (constructor.IsPrivate)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            }
            else if (constructor.IsFamily)
            {
                syntax = syntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword));
            }

            return syntax;
        }

        /// <summary>
        /// Returns the name of the provided parameter.
        /// If the parameter has no name (possible in F#),
        /// it returns a name computed by suffixing "arg" with the parameter's index
        /// </summary>
        /// <param name="parameter"> The parameter. </param>
        /// <param name="parameterIndex"> The parameter index in the list of parameters. </param>
        /// <returns> The parameter name. </returns>
        public static string GetOrCreateName(this ParameterInfo parameter, int parameterIndex)
        {
            var argName = parameter.Name;
            if (string.IsNullOrWhiteSpace(argName))
            {
                argName = string.Format(CultureInfo.InvariantCulture, "arg{0:G}", parameterIndex);
            }
            return argName;
        }

        /// <summary>
        /// Returns the parameter list syntax for the provided method.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The parameter list syntax for the provided method.
        /// </returns>
        public static ParameterSyntax[] GetParameterListSyntax(this MethodInfo method)
        {
            return
                method.GetParameters()
                    .Select(
                        (parameter, parameterIndex) =>
                        SyntaxFactory.Parameter(parameter.GetOrCreateName(parameterIndex).ToIdentifier())
                            .WithType(parameter.ParameterType.GetTypeSyntax()))
                    .ToArray();
        }

        /// <summary>
        /// Returns the parameter list syntax for the provided method.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The parameter list syntax for the provided method.
        /// </returns>
        public static TypeParameterSyntax[] GetTypeParameterListSyntax(this MethodInfo method)
        {
            return
                method.GetGenericArguments()
                      .Select(
                          (parameter) =>
                              SyntaxFactory.TypeParameter(parameter.Name))
                      .ToArray();
        }

        /// <summary>
        /// Returns the parameter list syntax for the provided constructor
        /// </summary>
        /// <param name="constructor">
        /// The constructor.
        /// </param>
        /// <returns>
        /// The parameter list syntax for the provided constructor.
        /// </returns>
        public static ParameterSyntax[] GetParameterListSyntax(this ConstructorInfo constructor)
        {
            return
                constructor.GetParameters()
                    .Select(
                        parameter =>
                        SyntaxFactory.Parameter(parameter.Name.ToIdentifier())
                            .WithType(parameter.ParameterType.GetTypeSyntax()))
                    .ToArray();
        }

        /// <summary>
        /// Returns type constraint syntax for the provided generic type argument.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// Name constraint syntax for the provided generic type argument.
        /// </returns>
        public static TypeParameterConstraintClauseSyntax[] GetTypeConstraintSyntax(this Type type)
        {
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsGenericTypeDefinition)
            {
                var constraints = new List<TypeParameterConstraintClauseSyntax>();
                foreach (var genericParameter in typeInfo.GetGenericArguments())
                {
                    constraints.AddRange(GetTypeParameterConstraints(genericParameter));
                }

                return constraints.ToArray();
            }

            return new TypeParameterConstraintClauseSyntax[0];
        }

        private static TypeParameterConstraintClauseSyntax[] GetTypeParameterConstraints(Type genericParameter)
        {
            var results = new List<TypeParameterConstraintClauseSyntax>();
            var parameterConstraints = new List<TypeParameterConstraintSyntax>();
            var attributes = genericParameter.GetTypeInfo().GenericParameterAttributes;

            // The "class" or "struct" constraints must come first.
            if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            {
                parameterConstraints.Add(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint));
            }
            else if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                parameterConstraints.Add(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint));
            }

            // Follow with the base class or interface constraints.
            foreach (var genericType in genericParameter.GetTypeInfo().GetGenericParameterConstraints())
            {
                // If the "struct" constraint was specified, skip the corresponding "ValueType" constraint.
                if (genericType == typeof(ValueType))
                {
                    continue;
                }

                parameterConstraints.Add(SyntaxFactory.TypeConstraint(genericType.GetTypeSyntax()));
            }

            // The "new()" constraint must be the last constraint in the sequence.
            if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
                && !attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                parameterConstraints.Add(SyntaxFactory.ConstructorConstraint());
            }

            if (parameterConstraints.Count > 0)
            {
                results.Add(
                    SyntaxFactory.TypeParameterConstraintClause(genericParameter.Name)
                                 .AddConstraints(parameterConstraints.ToArray()));
            }

            return results.ToArray();
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static MemberAccessExpressionSyntax Member(this ExpressionSyntax instance, string member)
        {
            return instance.Member(member.ToIdentifierName());
        }

        /// <summary>
        /// Returns qualified name syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static QualifiedNameSyntax Qualify(this NameSyntax instance, string member)
        {
            return instance.Qualify(member.ToIdentifierName());
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <param name="genericTypes">
        /// The generic type parameters.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static MemberAccessExpressionSyntax Member(
            this ExpressionSyntax instance,
            string member,
            params Type[] genericTypes)
        {
            return
                instance.Member(
                    member.ToGenericName()
                        .AddTypeArgumentListArguments(genericTypes.Select(_ => _.GetTypeSyntax()).ToArray()));
        }

        public static GenericNameSyntax ToGenericName(this string identifier)
        {
            return SyntaxFactory.GenericName(identifier.ToIdentifier());
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <typeparam name="TInstance">
        /// The class type.
        /// </typeparam>
        /// <typeparam name="T">
        /// The member return type.
        /// </typeparam>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <param name="genericTypes">
        /// The generic type parameters.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static MemberAccessExpressionSyntax Member<TInstance, T>(
            this ExpressionSyntax instance,
            Expression<Func<TInstance, T>> member,
            params Type[] genericTypes)
        {
            var methodCall = member.Body as MethodCallExpression;
            if (methodCall != null)
            {
                if (genericTypes != null && genericTypes.Length > 0)
                {
                    return instance.Member(methodCall.Method.Name, genericTypes);
                }

                return instance.Member(methodCall.Method.Name.ToIdentifierName());
            }

            var memberAccess = member.Body as MemberExpression;
            if (memberAccess != null)
            {
                if (genericTypes != null && genericTypes.Length > 0)
                {
                    return instance.Member(memberAccess.Member.Name, genericTypes);
                }

                return instance.Member(memberAccess.Member.Name.ToIdentifierName());
            }

            throw new ArgumentException("Expression type unsupported.");
        }

        /// <summary>
        /// Returns method invocation syntax.
        /// </summary>
        /// <typeparam name="T">
        /// The method return type.
        /// </typeparam>
        /// <param name="expression">
        /// The invocation expression.
        /// </param>
        /// <returns>
        /// The resulting <see cref="InvocationExpressionSyntax"/>.
        /// </returns>
        public static InvocationExpressionSyntax Invoke<T>(this Expression<Func<T>> expression)
        {
            var methodCall = expression.Body as MethodCallExpression;
            if (methodCall != null)
            {
                var decl = methodCall.Method.DeclaringType;
                return
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            decl.GetNameSyntax(),
                            methodCall.Method.GetNameSyntax()));
            }

            throw new ArgumentException("Expression type unsupported.");
        }

        /// <summary>
        /// Returns method invocation syntax.
        /// </summary>
        /// <param name="expression">
        /// The invocation expression.
        /// </param>
        /// <param name="instance">
        /// The instance to invoke this method on, or <see langword="null"/> for static invocation.
        /// </param>
        /// <returns>
        /// The resulting <see cref="InvocationExpressionSyntax"/>.
        /// </returns>
        public static InvocationExpressionSyntax Invoke(this Expression<Action> expression, ExpressionSyntax instance = null)
        {
            var methodCall = expression.Body as MethodCallExpression;
            if (methodCall != null)
            {
                if (instance == null && methodCall.Method.IsStatic)
                {
                    instance = methodCall.Method.DeclaringType.GetNameSyntax();
                }

                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        instance,
                        methodCall.Method.Name.ToIdentifierName()));
            }

            throw new ArgumentException("Expression type unsupported.");
        }

        /// <summary>
        /// Returns method invocation syntax.
        /// </summary>
        /// <typeparam name="T">The argument type of <paramref name="expression"/>.</typeparam>
        /// <param name="expression">
        /// The invocation expression.
        /// </param>
        /// <param name="instance">
        /// The instance to invoke this method on, or <see langword="null"/> for static invocation.
        /// </param>
        /// <returns>
        /// The resulting <see cref="InvocationExpressionSyntax"/>.
        /// </returns>
        public static InvocationExpressionSyntax Invoke<T>(this Expression<Action<T>> expression, ExpressionSyntax instance = null)
        {
            var methodCall = expression.Body as MethodCallExpression;
            if (methodCall != null)
            {
                var decl = methodCall.Method.DeclaringType;
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        instance ?? decl.GetNameSyntax(),
                        methodCall.Method.Name.ToIdentifierName()));
            }

            throw new ArgumentException("Expression type unsupported.");
        }

        public static IdentifierNameSyntax ToIdentifierName(this string identifier)
        {
            return SyntaxFactory.IdentifierName(identifier.ToIdentifier());
        }

        public static SyntaxToken ToIdentifier(this string identifier)
        {
            identifier = identifier.TrimStart('@');
            if (IsCSharpKeyword(identifier))
            {
                return SyntaxFactory.VerbatimIdentifier(
                    SyntaxTriviaList.Empty,
                    identifier,
                    identifier,
                    SyntaxTriviaList.Empty);
            }

            return SyntaxFactory.Identifier(SyntaxTriviaList.Empty, identifier, SyntaxTriviaList.Empty);
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static MemberAccessExpressionSyntax Member(this ExpressionSyntax instance, IdentifierNameSyntax member)
        {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, instance, member);
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static MemberAccessExpressionSyntax Member(this ExpressionSyntax instance, GenericNameSyntax member)
        {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, instance, member);
        }

        /// <summary>
        /// Returns member access syntax.
        /// </summary>
        /// <param name="instance">
        /// The instance.
        /// </param>
        /// <param name="member">
        /// The member.
        /// </param>
        /// <returns>
        /// The resulting <see cref="MemberAccessExpressionSyntax"/>.
        /// </returns>
        public static QualifiedNameSyntax Qualify(this NameSyntax instance, IdentifierNameSyntax member)
        {
            return SyntaxFactory.QualifiedName(instance, member).WithDotToken(SyntaxFactory.Token(SyntaxKind.DotToken));
        }

        private static string EscapeIdentifier(string identifier)
        {
            if (IsCSharpKeyword(identifier)) return "@" + identifier;
            return identifier;
        }

        internal static bool IsCSharpKeyword(string identifier)
        {
            switch (identifier)
            {
                case "abstract":
                case "add":
                case "alias":
                case "as":
                case "ascending":
                case "async":
                case "await":
                case "base":
                case "bool":
                case "break":
                case "byte":
                case "case":
                case "catch":
                case "char":
                case "checked":
                case "class":
                case "const":
                case "continue":
                case "decimal":
                case "default":
                case "delegate":
                case "descending":
                case "do":
                case "double":
                case "dynamic":
                case "else":
                case "enum":
                case "event":
                case "explicit":
                case "extern":
                case "false":
                case "finally":
                case "fixed":
                case "float":
                case "for":
                case "foreach":
                case "from":
                case "get":
                case "global":
                case "goto":
                case "group":
                case "if":
                case "implicit":
                case "in":
                case "int":
                case "interface":
                case "internal":
                case "into":
                case "is":
                case "join":
                case "let":
                case "lock":
                case "long":
                case "nameof":
                case "namespace":
                case "new":
                case "null":
                case "object":
                case "operator":
                case "orderby":
                case "out":
                case "override":
                case "params":
                case "partial":
                case "private":
                case "protected":
                case "public":
                case "readonly":
                case "ref":
                case "remove":
                case "return":
                case "sbyte":
                case "sealed":
                case "select":
                case "set":
                case "short":
                case "sizeof":
                case "stackalloc":
                case "static":
                case "string":
                case "struct":
                case "switch":
                case "this":
                case "throw":
                case "true":
                case "try":
                case "typeof":
                case "uint":
                case "ulong":
                case "unchecked":
                case "unsafe":
                case "ushort":
                case "using":
                case "value":
                case "var":
                case "virtual":
                case "void":
                case "volatile":
                case "when":
                case "where":
                case "while":
                case "yield":
                    return true;
                default:
                    return false;
            }
        }
    }
}
