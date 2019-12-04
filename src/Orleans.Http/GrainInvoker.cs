using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal sealed class GrainInvoker
    {
        private static readonly List<Type> _parameterAttributeTypes = new List<Type>{
            typeof(FromBodyAttribute),
            typeof(FromQueryAttribute)
        };
        private static readonly MethodInfo _getResultMethod = typeof(GrainInvoker).GetMethod(nameof(GetResult), BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly Type _taskOfTType = typeof(Task<>);
        private static readonly Type _grainHttpResultType = typeof(IGrainHttpResult);
        private readonly Dictionary<string, Parameter> _parameters = new Dictionary<string, Parameter>(StringComparer.OrdinalIgnoreCase);
        private readonly MethodInfo _methodInfo;
        private readonly ILogger _logger;
        private readonly MediaTypeManager _mediaTypeManager;
        private readonly RouteGrainProviderFactory _routeGrainProviderFactory;
        private MethodInfo _getResult;
        private bool _isIGrainHttpResultType;
        public Type GrainType => this._methodInfo.DeclaringType;

        private string _routeGrainProviderPolicy;
        public IRouteGrainProvider RouteGrainProvider
        {
            get
            {
                if(string.IsNullOrEmpty(this._routeGrainProviderPolicy))
                {
                    return this._routeGrainProviderFactory.CreateDefault();
                }
                return this._routeGrainProviderFactory.Create(this._routeGrainProviderPolicy);
            }
        }

        public GrainInvoker(IServiceProvider serviceProvider, MethodInfo methodInfo, string routeGrainProviderPolicy)
        {
            this._logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<GrainInvoker>();
            this._methodInfo = methodInfo;
            this._mediaTypeManager = serviceProvider.GetRequiredService<MediaTypeManager>();
            this._routeGrainProviderFactory = serviceProvider.GetRequiredService<RouteGrainProviderFactory>();
            this._routeGrainProviderPolicy = routeGrainProviderPolicy;

            this.BuildResultDelegate();
            this.BuildParameterMap();
        }

        public async Task Invoke(IGrain grain, HttpContext context)
        {
            var grainCall = (Task)this._methodInfo.Invoke(grain, await this.GetParameters(context));
            await grainCall;

            if (this._getResult != null)
            {
                object result = this._getResult.Invoke(null, new[] { grainCall });

                if (result != null)
                {
                    string contentType = string.Empty;
                    if (context.Request.Headers.TryGetValue("accept", out StringValues val))
                    {
                        contentType = val.FirstOrDefault();
                    }
                    else
                    {
                        contentType = context.Request.ContentType;
                    }
                    context.Response.ContentType = contentType;

                    if (this._isIGrainHttpResultType)
                    {
                        var httpResult = (IGrainHttpResult)result;
                        context.Response.StatusCode = (int)httpResult.StatusCode;
                        if (httpResult.ResponseHeaders?.Count > 0)
                        {
                            foreach (var header in httpResult.ResponseHeaders)
                            {
                                context.Response.Headers[header.Key] = header.Value;
                            }
                        }

                        if (httpResult.Body != null)
                        {
                            var serialized = await this._mediaTypeManager.Serialize(contentType, httpResult.Body, context.Response.BodyWriter);
                            if (!serialized)
                            {
                                await context.Response.WriteAsync(httpResult.Body.ToString());
                            }
                        }
                    }
                    else
                    {
                        var serialized = await this._mediaTypeManager.Serialize(contentType, result, context.Response.BodyWriter);
                        if (!serialized)
                        {
                            await context.Response.WriteAsync(result.ToString());
                        }
                    }
                }
            }
        }

        private async ValueTask<object[]> GetParameters(HttpContext context)
        {
            object[] parameterValues = default;
            if (this._parameters.Count > 0)
            {
                parameterValues = new object[this._parameters.Count];
                var routeParameters = context.Request.RouteValues;

                for (int i = 0; i < this._parameters.Count; i++)
                {
                    var param = this._parameters.ElementAt(i).Value;
                    if (param.Source == ParameterSource.Body)
                    {
                        parameterValues[i] = await this.ParseParameter(param, context);
                    }
                    else if (param.Source == ParameterSource.Route &&
                        routeParameters.TryGetValue(param.Name, out object routeParam))
                    {
                        parameterValues[i] = this.ParseParameter(param, routeParam);
                    }
                    else if (param.Source == ParameterSource.Query &&
                        context.Request.Query.TryGetValue(param.Name, out var query) &&
                        query.Count > 0)
                    {
                        var queryValue = query.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(queryValue))
                        {
                            parameterValues[i] = this.ParseParameter(param, queryValue);
                        }
                    }
                }
            }
            return parameterValues;
        }

        private object ParseParameter(Parameter parameterType, object parameterValue)
        {
            // HACK: Remove this insanely slow hack!
            if (parameterType.Type == typeof(int) &&
                int.TryParse((string)parameterValue, out int intValue))
            {
                return intValue;
            }
            else if (parameterType.Type == typeof(long) &&
                long.TryParse((string)parameterValue, out long longValue))
            {
                return longValue;
            }
            else if (parameterType.Type == typeof(decimal) &&
                decimal.TryParse((string)parameterValue, out decimal decimalValue))
            {
                return decimalValue;
            }
            else if (parameterType.Type == typeof(double) &&
                double.TryParse((string)parameterValue, out double doubleValue))
            {
                return doubleValue;
            }
            else if (parameterType.Type == typeof(float) &&
                float.TryParse((string)parameterValue, out float floatValue))
            {
                return floatValue;
            }
            if (parameterType.Type == typeof(byte) &&
                byte.TryParse((string)parameterValue, out byte byteValue))
            {
                return byteValue;
            }
            else if (parameterType.Type == typeof(bool) &&
                bool.TryParse((string)parameterValue, out bool boolValue))
            {
                return boolValue;
            }
            else if (parameterType.Type == typeof(Guid) &&
                Guid.TryParse((string)parameterValue, out Guid guidValue))
            {
                return guidValue;
            }
            else if (parameterType.Type == typeof(DateTime) &&
                DateTime.TryParse((string)parameterValue, out DateTime dateTimeValue))
            {
                // FIXME: How to consider the datetime format?
                return dateTimeValue;
            }
            else if (parameterType.Type == typeof(char) &&
                char.TryParse((string)parameterValue, out char charValue))
            {
                return charValue;
            }
            else
            {
                return parameterValue;
            }
        }

        private async ValueTask<object> ParseParameter(Parameter parameterType, HttpContext context)
        {
            try
            {
                return await this._mediaTypeManager.Deserialize(context.Request.ContentType, context.Request.BodyReader, parameterType.Type, context.RequestAborted);
            }
            catch (Exception exc)
            {
                this._logger.LogError(exc, $"Unable to deserialize parameter '{parameterType.Name}' from request body: {exc.Message}.");
                return null;
            }
        }

        private void BuildParameterMap()
        {
            var methodParams = this._methodInfo.GetParameters();
            var alreadyHasBody = false;
            foreach (var methodParameter in methodParams)
            {
                if (methodParameter.Name == Constants.GRAIN_ID || methodParameter.Name == Constants.GRAIN_ID_EXTENSION) continue;

                var attribute = methodParameter.GetCustomAttributes()
                    .Where(attr => _parameterAttributeTypes.Contains(attr.GetType())).FirstOrDefault();

                ParameterSource source = default;

                if (attribute != null)
                {
                    if (attribute is FromBodyAttribute)
                    {
                        if (alreadyHasBody) throw new InvalidOperationException("A method can only have 1 body parameter.");
                        source = ParameterSource.Body;
                        alreadyHasBody = true;
                    }
                    else if (attribute is FromQueryAttribute)
                    {
                        source = ParameterSource.Query;
                    }
                }
                else
                {
                    source = ParameterSource.Route;
                }

                this._parameters[methodParameter.Name] = new Parameter(methodParameter.Name, methodParameter.ParameterType, source);
            }
        }

        private void BuildResultDelegate()
        {
            if (this._methodInfo.ReturnType.IsGenericType &&
                this._methodInfo.ReturnType.GetGenericTypeDefinition() == _taskOfTType)
            {
                var returnType = this._methodInfo.ReturnType.GenericTypeArguments[0];
                if (returnType == _grainHttpResultType || returnType.GetInterfaces().Any(i => i == _grainHttpResultType))
                {
                    this._isIGrainHttpResultType = true;
                }
                this._getResult = _getResultMethod.MakeGenericMethod(returnType);
            }
        }

        private static object GetResult<T>(Task<T> input) => (object)input.GetAwaiter().GetResult();

        private readonly struct Parameter
        {
            public string Name { get; }
            public Type Type { get; }
            public ParameterSource Source { get; }

            public Parameter(string name, Type type, ParameterSource source)
            {
                this.Name = name;
                this.Type = type;
                this.Source = source;
            }
        }

        private enum ParameterSource
        {
            Body = 0,
            Query = 1,
            Route = 2
        }
    }
}
