using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Orleans.Http
{
    internal sealed class GrainInvoker
    {
        private static readonly MethodInfo _getResultMethod = typeof(GrainInvoker).GetMethod(nameof(GetResult), BindingFlags.Static | BindingFlags.NonPublic);

        private readonly IClusterClient _clusterClient;
        private readonly MethodInfo _methodInfo;
        private MethodInfo _getResult;
        public Type GrainType => this._methodInfo.DeclaringType;
        public GrainIdType GrainIdType { get; private set; }

        public GrainInvoker(IClusterClient clusterClient, GrainIdType grainIdType, MethodInfo methodInfo)
        {
            this.GrainIdType = grainIdType;
            this._clusterClient = clusterClient;
            this._methodInfo = methodInfo;

            this.BuildResultDelegate();
        }

        public async Task Invoke(IGrain grain, HttpContext context)
        {
            // TODO: Implement proper parameter binding
            var grainCall = (Task)this._methodInfo.Invoke(grain, null);
            await grainCall;

            if (this._getResult != null)
            {
                object result = this._getResult.Invoke(null, new[] { grainCall });
                if (result != null)
                {
                    // TODO: Check if it is a complex type before serializinng
                    // TODO: Support external serializers mapped to the application type
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
                }
            }
        }

        private void BuildResultDelegate()
        {
            if (this._methodInfo.ReturnType.IsGenericType &&
                            this._methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                this._getResult = _getResultMethod.MakeGenericMethod(this._methodInfo.ReturnType.GenericTypeArguments[0]);
            }
        }

        private static object GetResult<T>(Task<T> input) => (object)input.GetAwaiter().GetResult();
    }
}