using Microsoft.Owin;
using Newtonsoft.Json;
using Orleans;
using Orleans.Providers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestGrains;

namespace OrleansHttp
{

    public class GrainController : GrainCaller
    {
        public GrainController(Router router, TaskScheduler taskScheduler, IProviderRuntime providerRuntime) : base(taskScheduler, providerRuntime.GrainFactory)
        {
            Action<string, Func<IOwinContext, IDictionary<string, string>, Task>> add = router.Add;

            add("/grain/:type/:id/:method", CallGrain);
            add("/grain/:type/:id/:method/:classprefix", CallGrain);
            add("/pinggrain", PingGrain);
            add("/ping", Ping);
        }

        public Task Ping(IOwinContext context, IDictionary<string, string> parameters)
        {
            return TaskDone.Done;    
        }

        public async Task PingGrain(IOwinContext context, IDictionary<string, string> parameters)
        {
            var result = await Dispatch(async () =>
            {
                var grain = GrainFactory.GetGrain<ITestGrain>("0");
                await grain.Test();
                return null;
            });
        }



        public async Task CallGrain(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grainTypeName = parameters["type"];
            var grainId = parameters["id"];
            var classPrefix = parameters.ContainsKey("classprefix") ? parameters["classprefix"] : null;
            var grainMethodName = parameters["method"];

            var grainType = GetGrainType(grainTypeName);
            var grainFactory = GetGrainFactoryWithCache(grainTypeName);
            var grainMethod = this.grainMethodCache.GetOrAdd($"{grainTypeName}.{grainMethodName}", x => grainType.GetImpMethod(grainMethodName));
            var grainMethodParams = GetGrainParameters(grainMethod, context).ToArray();

            var grain = GetGrain(grainType, grainFactory, grainId, classPrefix);

            if (null == grainMethod) throw new MissingMethodException(grainTypeName, grainMethodName);

            var result = await CallGrain(grainType, grain, grainMethod, grainMethodParams);
       
            await context.ReturnJson(result);
        }


        public IEnumerable<object> GetGrainParameters(MethodInfo grainMethod, IOwinContext context)
        {
            foreach (var param in grainMethod.GetParameters())
            {
                var value = context.Request.Query[param.Name];
                if (null == value)
                {
                    yield return null;
                    continue;
                }
                yield return JsonConvert.DeserializeObject(value, param.ParameterType, ExtensionMethods.jsonSerializerSettings);
            }
        }

    }
}
