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

    public class GrainController
    {
        public TaskScheduler TaskScheduler { get; private set; }
        public IProviderRuntime ProviderRuntime { get; private set; }
        ConcurrentDictionary<string, MethodInfo> grainFactoryCache = new ConcurrentDictionary<string, MethodInfo>();
        ConcurrentDictionary<string, MethodInfo> grainMethodCache = new ConcurrentDictionary<string, MethodInfo>();
        ConcurrentDictionary<string, Type> grainTypeCache = new ConcurrentDictionary<string, Type>();

        public GrainController(Router router, TaskScheduler taskScheduler, IProviderRuntime providerRuntime)
        {
            this.TaskScheduler = taskScheduler;
            this.ProviderRuntime = providerRuntime;

            Action<string, Func<IOwinContext, IDictionary<string, string>, Task>> add = router.Add;

            add("/grain/:type/:id/:method", CallGrain);
            add("/grain/:type/:id/:method/:classprefix", CallGrain);
            add("/pinggrain", PingGrain);
            add("/ping", Ping);
        }

        Task Ping(IOwinContext context, IDictionary<string, string> parameters)
        {
            return TaskDone.Done;    
        }

        async Task PingGrain(IOwinContext context, IDictionary<string, string> parameters)
        {
            var result = await Dispatch(async () =>
            {
                var grain = ProviderRuntime.GrainFactory.GetGrain<ITestGrain>("0");
                await grain.Test();
                return null;
            });
        }



        async Task CallGrain(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grainTypeName = parameters["type"];
            var grainId = parameters["id"];
            var classPrefix = parameters.ContainsKey("classprefix") ? parameters["classprefix"] : null;
            var grainMethodName = parameters["method"];

            var grainType = GetGrainType(grainTypeName);
            var grainFactory = GetGrainFactoryWithCache(grainTypeName);

            var grain = GetGrain(grainType, grainFactory, grainId, classPrefix);

            var grainMethod = this.grainMethodCache.GetOrAdd($"{grainTypeName}.{grainMethodName}", x => grainType.GetMethod(grainMethodName));
            if (null == grainMethod) throw new MissingMethodException(grainTypeName, grainMethodName);

            var grainMethodParams = GetGrainParameters(grainMethod, context).ToArray();
          
            var result = await Dispatch(async () =>
            {
                var task = grainMethod.Invoke(grain, grainMethodParams) as Task;
                await task;

                // hack, as we can't cast task<int> to task<object>
                var resultProperty = task.GetType().GetProperties().FirstOrDefault(x => x.Name == "Result");
                if (null != resultProperty) return resultProperty.GetValue(task);
                return null;
            });
       
            await context.ReturnJson(result);
        }


        IEnumerable<object> GetGrainParameters(MethodInfo grainMethod, IOwinContext context)
        {
            foreach (var param in grainMethod.GetParameters())
            {
                var value = context.Request.Query[param.Name];
                if (null == value)
                {
                    yield return null;
                    continue;
                }
                yield return JsonConvert.DeserializeObject(value, param.ParameterType);
            }
        }

        Task<object> Dispatch(Func<Task<object>> func)
        {
            return Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, scheduler: this.TaskScheduler).Result;
        }

        // horrible way of getting the correct method to get a grain reference
        // this could be optimised further by returning this as a closure when getting the factory methodinfo
        object GetGrain(Type grainType, MethodInfo grainFactoryMethod, string id, string classPrefix)
        {
            if (typeof(IGrainWithGuidKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { Guid.Parse(id), classPrefix });
            }
            if (typeof(IGrainWithIntegerKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { long.Parse(id), classPrefix });
            }

            if (typeof(IGrainWithStringKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { id, classPrefix });
            }

            if (typeof(IGrainWithGuidCompoundKey).IsAssignableFrom(grainType))
            {
                var parts = id.Split(',');
                return grainFactoryMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { Guid.Parse(parts[0]), parts[1], classPrefix });
            }
            if (typeof(IGrainWithIntegerCompoundKey).IsAssignableFrom(grainType))
            {
                var parts = id.Split(',');
                return grainFactoryMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { long.Parse(parts[0]), parts[1], classPrefix });
            }

            throw new NotSupportedException($"cannot construct grain {grainType.Name}");
        }


        Type GetGrainType(string grainTypeName)
        {
            return grainTypeCache.GetOrAdd(grainTypeName, GetGrainTypeViaReflection);
        }

        Type GetGrainTypeViaReflection(string grainTypeName)
        {
            var grainType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.Name == grainTypeName).FirstOrDefault();
            if (null == grainType) throw new ArgumentException($"Grain type not found '{grainTypeName}'");
            return grainType;
        }


        MethodInfo GetGrainFactoryWithCache(string grainTypeName)
        {
            return this.grainFactoryCache.GetOrAdd(grainTypeName, GetGrainFactoryViaReflection);
        }

        MethodInfo GetGrainFactoryViaReflection(string grainTypeName)
        {
            var grainType = GetGrainType(grainTypeName);
            var methods = this.ProviderRuntime.GrainFactory.GetType().GetMethods().Where(x => x.Name == "GetGrain");

            if (typeof(IGrainWithGuidKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "System.Guid");
                return method.MakeGenericMethod(grainType);
            }
            if (typeof(IGrainWithIntegerKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "Int64");
                return method.MakeGenericMethod(grainType);
            }

            if (typeof(IGrainWithStringKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "String");
                return method.MakeGenericMethod(grainType);
            }

            if (typeof(IGrainWithGuidCompoundKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 3 && x.GetParameters().First().ParameterType.Name == "System.Guid");
                return method.MakeGenericMethod(grainType);
            }
            if (typeof(IGrainWithIntegerCompoundKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 3 && x.GetParameters().First().ParameterType.Name == "Int64");
                return method.MakeGenericMethod(grainType);
            }

            throw new NotSupportedException($"cannot construct grain {grainType.Name}");
        }

    }
}
