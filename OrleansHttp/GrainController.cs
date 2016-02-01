using Microsoft.Owin;
using Newtonsoft.Json;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OrleansHttp
{

    public class GrainController 
    {
        public TaskScheduler TaskScheduler { get; private set; }
        public IProviderRuntime ProviderRuntime { get; private set; }


        public GrainController(Router router, TaskScheduler taskScheduler, IProviderRuntime providerRuntime)
        {
            this.TaskScheduler = taskScheduler;
            this.ProviderRuntime = providerRuntime;

            Action<string, Func<IOwinContext, IDictionary<string, string>, Task>> add = router.Add;

            add("/grain/:type/:id/:method", CallGrain);
            add("/grain/:type/:id/:method/:classprefix", CallGrain);
        }

        async Task CallGrain(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grainTypeName = parameters["type"];

            // consider caching this lookup
            var grainType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.Name == grainTypeName).FirstOrDefault();
            if (null == grainType) throw new ArgumentException($"Grain type not found '{grainTypeName}'");

            var grain = GetGrain(grainType, parameters["id"], parameters.ContainsKey("classprefix") ? parameters["classprefix"] : null);

            var grainMethod = grainType.GetMethod(parameters["method"]);
            if (null == grainMethod) throw new MissingMethodException(grainTypeName, parameters["method"]);
        
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
        object GetGrain(Type grainType, string id, string classPrefix)
        {
            var methods = this.ProviderRuntime.GrainFactory.GetType().GetMethods().Where(x => x.Name == "GetGrain");
            

            if (typeof(IGrainWithGuidKey).IsAssignableFrom(grainType))
            {
                var method = methods.Where(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "System.Guid").First();
                var genericMethod = method.MakeGenericMethod(grainType);
                return genericMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { Guid.Parse(id), classPrefix });
            }
            if (typeof(IGrainWithIntegerKey).IsAssignableFrom(grainType))
            {
                var method = methods.Where(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "Int64").First();
                var genericMethod = method.MakeGenericMethod(grainType);
                return genericMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { long.Parse(id), classPrefix });
            }

            if (typeof(IGrainWithStringKey).IsAssignableFrom(grainType))
            {
                var method = methods.Where(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.Name == "String").First();
                var genericMethod = method.MakeGenericMethod(grainType);
                return genericMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { id, classPrefix });
            }

            if (typeof(IGrainWithGuidCompoundKey).IsAssignableFrom(grainType))
            {
                var method = methods.Where(x => x.GetParameters().Length == 3 && x.GetParameters().First().ParameterType.Name == "System.Guid").First();
                var genericMethod = method.MakeGenericMethod(grainType);
                var parts = id.Split(',');
                return genericMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { Guid.Parse(parts[0]), parts[1] , classPrefix });
            }
            if (typeof(IGrainWithIntegerCompoundKey).IsAssignableFrom(grainType))
            {
                var method = methods.Where(x => x.GetParameters().Length == 3 && x.GetParameters().First().ParameterType.Name == "Int64").First();
                var genericMethod = method.MakeGenericMethod(grainType);
                var parts = id.Split(',');
                return genericMethod.Invoke(this.ProviderRuntime.GrainFactory, new object[] { long.Parse(parts[0]), parts[1], classPrefix });
            }

            throw new NotSupportedException($"cannot construct grain {grainType.Name}");
        }

    }
}
