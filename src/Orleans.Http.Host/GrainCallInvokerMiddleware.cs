using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Orleans.Runtime;
using Orleans.Http.Execution;
using Orleans.Http.Metadata;

namespace Orleans.Http.Host
{
    public class GrainCallInvokerMiddleware : IMiddleware
    {
        private readonly IClusterClient clusterClient;
        private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
        private readonly Dictionary<string, GrainDescription> grains;
        private readonly IMethodCallDispatcher dispatcher;

        public GrainCallInvokerMiddleware(IClusterClient clusterClient, GrainMetadataCollection metadata)
        {
            this.clusterClient = clusterClient;
            this.grains = metadata.Grains;
            this.dispatcher = metadata.Dispatcher;
        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var command = JsonConvert.DeserializeObject<MethodCall>(body);

            var targetType = command?.Target.Type;
            if (targetType == null || !this.grains.TryGetValue(targetType, out var grain))
            {
                await WriteFailure("Grain type not specified.");
                return;
            }

            if (!grain.Methods.TryGetValue(command.MethodName, out var method))
            {
                await WriteFailure(
                    $"Method \"{command.MethodName}\" not found on grain \"{command.Target.Type}\".",
                    404);
                return;
            }

            var inArgs = command.Arguments?.Length ?? 0;
            var reqArgs = method.Args?.Count ?? 0;
            if (inArgs != reqArgs)
            {
                await WriteFailure($"Incorrect number of arguments. Received {inArgs} but expected {reqArgs}. Method: {method}");
                return;
            }

            var resultTask = this.dispatcher.Dispatch(this.clusterClient, command);
            if (resultTask == null)
            {
                await WriteFailure(
                    "The specified method does not exist. Input: "
                    + JsonConvert.SerializeObject(command, this.jsonSettings),
                    404);
                return;
            }

            object result;
            try
            {
                result = await resultTask;
            }
            catch (OrleansException exception)
            {
                await WriteFailure(exception.Message, 500);
                return;
            }
            catch (Exception exception)
            {
                await WriteFailure(exception.Message);
                return;
            }

            await WriteSuccess(result);

            Task WriteSuccess(object resultObj)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var resultString = JsonConvert.SerializeObject(resultObj);
                return context.Response.WriteAsync(resultString, context.RequestAborted);
            }

            Task WriteFailure(string error, int statusCode = 400)
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/text";
                return context.Response.WriteAsync(error, context.RequestAborted);
            }
        }
    }
}