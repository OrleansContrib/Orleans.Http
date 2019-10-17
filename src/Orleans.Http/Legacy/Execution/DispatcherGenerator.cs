using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orleans.Http.Exceptions;
using Orleans.Http.Metadata;
using Orleans.Http.Utilities;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Orleans.Http.Execution
{
    /// <summary>
    /// Code generator for routing method calls to grains.
    /// </summary>
    public static class DispatcherGenerator
    {
        /// <summary>
        /// The class suffix.
        /// </summary>
        private const string CommandDispatcher = "CommandDispatcher";

        /// <summary>
        /// Gets a dispatcher for the provided <paramref name="grains"/>.
        /// </summary>
        /// <param name="grains">
        /// The grains.
        /// </param>
        /// <param name="source">
        /// The generated source code.
        /// </param>
        /// <returns>
        /// A dispatcher for the provided <paramref name="grains"/>.
        /// </returns>
        /// <exception cref="CodeGenerationException">
        /// A code generation error occurred.
        /// </exception>
        public static IMethodCallDispatcher GetDispatcher(IList<GrainDescription> grains, out string source)
        {
            var assemblies =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(asm => !asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
                    .Select(asm => MetadataReference.CreateFromFile(asm.Location))
                    .Cast<MetadataReference>()
                    .ToArray();

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var syntax = GenerateCompilationUnit(grains).NormalizeWhitespace();
            source = syntax.ToFullString();

            var compilation =
                CSharpCompilation.Create("CodeGen_" + CommandDispatcher + DateTime.UtcNow.Ticks.ToString("X") + ".dll")
                    .AddSyntaxTrees(GenerateCompilationUnit(grains).SyntaxTree)
                    .AddReferences(assemblies)
                    .WithOptions(options);

            Assembly compiledAssembly;
            using (var stream = new MemoryStream())
            {
                var compilationResult = compilation.Emit(stream);
                if (!compilationResult.Success)
                {
                    throw new CodeGenerationException(
                        string.Join("\n", compilationResult.Diagnostics.Select(_ => _.ToString())));
                }

                compiledAssembly = Assembly.Load(stream.GetBuffer());
            }

            var dispatcher = compiledAssembly.GetTypes().Single(typeof(IMethodCallDispatcher).IsAssignableFrom);
            return (IMethodCallDispatcher)Activator.CreateInstance(dispatcher);
        }

        /// <summary>
        /// Returns compilation unit syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </summary>
        /// <param name="grains">
        /// The grain descriptions.
        /// </param>
        /// <returns>
        /// Compilation unit syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </returns>
        private static CompilationUnitSyntax GenerateCompilationUnit(IList<GrainDescription> grains)
        {
            var ns =
                NamespaceDeclaration(ParseName("Generated" + DateTime.UtcNow.Ticks))
                    .AddMembers(GenerateClass(grains));
            return CompilationUnit().AddMembers(ns);
        }

        /// <summary>
        /// Returns class syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </summary>
        /// <param name="grains">
        /// The grain descriptions.
        /// </param>
        /// <returns>
        /// Class syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </returns>
        private static TypeDeclarationSyntax GenerateClass(IEnumerable<GrainDescription> grains)
        {
            var eventDispatcher = SimpleBaseType(typeof(IMethodCallDispatcher).GetTypeSyntax());
            return
                ClassDeclaration(CommandDispatcher)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddBaseListTypes(eventDispatcher)
                    .AddMembers(GenerateMethod(grains));
        }

        /// <summary>
        /// Returns method syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </summary>
        /// <param name="grains">
        /// The grain descriptions.
        /// </param>
        /// <returns>
        /// Method syntax for dispatching events to the provided <paramref name="grains"/>.
        /// </returns>
        private static MethodDeclarationSyntax GenerateMethod(IEnumerable<GrainDescription> grains)
        {
            // Types
            var clientType = typeof(IClusterClient).GetTypeSyntax();
            var eventType = typeof(MethodCall).GetTypeSyntax();
            var returnType = typeof(Task<object>).GetTypeSyntax();

            // Local variables
            var client = IdentifierName("client");
            var command = IdentifierName("command");

            // Parameters
            var clientParam = Parameter(client.Identifier).WithType(clientType);
            var commandParam = Parameter(command.Identifier).WithType(eventType);

            // Body statements
            var returnNull = ReturnStatement(LiteralExpression(SyntaxKind.NullLiteralExpression));
            var defaultSection = SwitchSection().AddLabels(DefaultSwitchLabel()).AddStatements(returnNull);
            var grainTypeSwitch =
                SwitchStatement(command.Member("Target").Member("Type"))
                    .AddSections(
                        grains.Where(grain => grain.Methods.Any())
                            .Select(grain => GetGrainSwitch(grain, command))
                            .ToArray())
                    .AddSections(defaultSection);

            // Build and return the method.
            return
                MethodDeclaration(returnType, "Dispatch")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword))
                    .AddParameterListParameters(clientParam, commandParam)
                    .AddBodyStatements(grainTypeSwitch);
        }

        /// <summary>
        /// Returns syntax for dispatching <paramref name="command"/> to <paramref name="grain"/>.
        /// </summary>
        /// <param name="grain">
        /// The grain description.
        /// </param>
        /// <param name="command">
        /// The event.
        /// </param>
        /// <returns>
        /// Syntax for dispatching <paramref name="command"/> to <paramref name="grain"/>.
        /// </returns>
        private static SwitchSectionSyntax GetGrainSwitch(
            GrainDescription grain, 
            ExpressionSyntax command)
        {
            var label =
                CaseSwitchLabel(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(grain.Kind)));
            return
                SwitchSection()
                    .AddLabels(label)
                    .AddStatements(GenerateGrainBlock(command, grain));
        }

        /// <summary>
        /// Returns syntax for dispatching <paramref name="command"/> to <paramref name="grainDescription"/>.
        /// </summary>
        /// <param name="command">
        /// The event.
        /// </param>
        /// <param name="grainDescription">
        /// The grain description.
        /// </param>
        /// <returns>
        /// Syntax for dispatching <paramref name="command"/> to <paramref name="grainDescription"/>.
        /// </returns>
        private static StatementSyntax GenerateGrainBlock(
            ExpressionSyntax command, 
            GrainDescription grainDescription)
        {
            var grainType = grainDescription.Type;
            var getGrain =
                InvocationExpression(IdentifierName("client").Member("GetGrain", grainType))
                    .AddArgumentListArguments(Argument(command.Member("Target").Member("Id")));
            var grain = VariableDeclarator("grain").WithInitializer(EqualsValueClause(getGrain));
            var grainDeclaration = LocalDeclarationStatement(VariableDeclaration(IdentifierName("var")).AddVariables(grain));

            var returnNull = ReturnStatement(LiteralExpression(SyntaxKind.NullLiteralExpression));
            
            var defaultSection = SwitchSection().AddLabels(DefaultSwitchLabel()).AddStatements(returnNull);
            var methodSwitch =
                SwitchStatement(command.Member("MethodName"))
                    .AddSections(
                        grainDescription.Methods.Values.Where(_ => _.Visible)
                            .Select(method => GetMethodSwitchCase(command, method))
                            .ToArray())
                    .AddSections(defaultSection);
            var methodDispatcher = Block().AddStatements(grainDeclaration, methodSwitch);
            return methodDispatcher;
        }

        /// <summary>
        /// Returns syntax for dispatching <paramref name="command"/> to <paramref name="method"/>.
        /// </summary>
        /// <param name="command">
        /// The event.
        /// </param>
        /// <param name="method">
        /// The grain description.
        /// </param>
        /// <returns>
        /// Syntax for dispatching <paramref name="command"/> to <paramref name="method"/>.
        /// </returns>
        private static SwitchSectionSyntax GetMethodSwitchCase(ExpressionSyntax command, GrainMethodDescription method)
        {
            var label =
                CaseSwitchLabel(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(method.Name)));
            return
                SwitchSection()
                    .AddLabels(label)
                    .AddStatements(
                        GenerateMethodDispatcher(command, IdentifierName("grain"), method.MethodInfo));
        }

        /// <summary>
        /// Returns syntax for dispatching <paramref name="command"/> to <paramref name="method"/> on <paramref name="grain"/>.
        /// </summary>
        /// <param name="command">
        /// The event.
        /// </param>
        /// <param name="grain">
        /// The grain.
        /// </param>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// Syntax for dispatching <paramref name="command"/> to <paramref name="method"/> on <paramref name="grain"/>.
        /// </returns>
        private static StatementSyntax[] GenerateMethodDispatcher(
            ExpressionSyntax command, 
            ExpressionSyntax grain, 
            MethodInfo method)
        {
            // Construct expressions to retrieve each of the method's parameters, starting with the 'self' parameter.
            var parameters = new List<ExpressionSyntax>();
            var methodParameters = method.GetParameters().ToList();
            for (var i = 0; i < methodParameters.Count; i++)
            {
                var parameter = methodParameters[i];
                var parameterType = parameter.ParameterType;
                var indexArg =
                    Argument(
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)));
                var getArg =
                    InvocationExpression(command.Member("Arg", parameterType)).AddArgumentListArguments(indexArg);
                parameters.Add(getArg);
            }

            var grainMethodCall =
                AwaitExpression(InvocationExpression(grain.Member(method.Name))
                    .AddArgumentListArguments(parameters.Select(Argument).ToArray()));

            if (!method.ReturnType.IsGenericType)
            {
                return new StatementSyntax[]
                {
                    ExpressionStatement(grainMethodCall),
                    ReturnStatement(LiteralExpression(SyntaxKind.NullLiteralExpression))
                };
            }

            return new StatementSyntax[] {ReturnStatement(grainMethodCall)};
        }
    }
}