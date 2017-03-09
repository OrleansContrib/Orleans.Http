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

    public class GrainCaller
    {
        public static GrainCaller Instance;
        public TaskScheduler TaskScheduler { get; private set; }
        public IGrainFactory GrainFactory { get; private set; }
        public ConcurrentDictionary<string, MethodInfo> grainFactoryCache = new ConcurrentDictionary<string, MethodInfo>();
        public ConcurrentDictionary<string, MethodInfo> grainMethodCache = new ConcurrentDictionary<string, MethodInfo>();
        public ConcurrentDictionary<string, Type> grainTypeCache = new ConcurrentDictionary<string, Type>();

        public GrainCaller(TaskScheduler taskScheduler, IGrainFactory GrainFactory)
        {
            Instance = this;
            this.TaskScheduler = taskScheduler;
            this.GrainFactory = GrainFactory;
        }

        public async Task<object> CallGrain(Type grainType, Object grain, MethodInfo grainMethod, object[] grainMethodParams)
        {
            var result = await Dispatch(async () =>
            {
                var task = grainMethod.Invoke(grain, grainMethodParams) as Task;
                await task;

                // hack, as we can't cast task<int> to task<object>
                var resultProperty = task.GetType().GetProperties().FirstOrDefault(x => x.Name == "Result");
                if (null != resultProperty) return resultProperty.GetValue(task);
                return null;
            });
            return result;
        }
        

        public async Task<object> Dispatch(Func<Task<object>> func)
        {
            return await Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, scheduler: this.TaskScheduler);
        }

        // horrible way of getting the correct method to get a grain reference
        // this could be optimised further by returning this as a closure when getting the factory methodinfo
        public object GetGrain(Type grainType, MethodInfo grainFactoryMethod, string id, string classPrefix)
        {
            if (typeof(IGrainWithGuidKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.GrainFactory, new object[] { Guid.Parse(id), classPrefix });
            }
            if (typeof(IGrainWithIntegerKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.GrainFactory, new object[] { long.Parse(id), classPrefix });
            }

            if (typeof(IGrainWithStringKey).IsAssignableFrom(grainType))
            {
                return grainFactoryMethod.Invoke(this.GrainFactory, new object[] { id, classPrefix });
            }

            if (typeof(IGrainWithGuidCompoundKey).IsAssignableFrom(grainType))
            {
                var parts = id.Split(',');
                return grainFactoryMethod.Invoke(this.GrainFactory, new object[] { Guid.Parse(parts[0]), parts[1], classPrefix });
            }
            if (typeof(IGrainWithIntegerCompoundKey).IsAssignableFrom(grainType))
            {
                var parts = id.Split(',');
                return grainFactoryMethod.Invoke(this.GrainFactory, new object[] { long.Parse(parts[0]), parts[1], classPrefix });
            }

            throw new NotSupportedException($"cannot construct grain {grainType.Name}");
        }


        public Type GetGrainType(string grainTypeName)
        {
            return grainTypeCache.GetOrAdd(grainTypeName, GetGrainTypeViaReflection);
        }

        public Type GetGrainTypeViaReflection(string grainTypeName)
        {
            var grainType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.Name == grainTypeName).FirstOrDefault();
            if (null == grainType) throw new ArgumentException($"Grain type not found '{grainTypeName}'");
            return grainType;
        }


        public MethodInfo GetGrainFactoryWithCache(string grainTypeName)
        {
            return this.grainFactoryCache.GetOrAdd(grainTypeName, GetGrainFactoryViaReflection);
        }

        public MethodInfo GetGrainFactoryViaReflection(string grainTypeName)
        {
            var grainType = GetGrainType(grainTypeName);
            var methods = this.GrainFactory.GetType().GetMethods().Where(x => x.Name == "GetGrain");

            if (typeof(IGrainWithGuidKey).IsAssignableFrom(grainType))
            {
                var method = methods.First(x => x.GetParameters().Length == 2 && x.GetParameters().First().ParameterType.FullName == "System.Guid");
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
                var method = methods.First(x => x.GetParameters().Length == 3 && x.GetParameters().First().ParameterType.FullName == "System.Guid");
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
