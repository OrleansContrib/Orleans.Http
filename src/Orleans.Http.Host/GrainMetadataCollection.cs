using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orleans.ApplicationParts;
using Orleans.Http.Execution;
using Orleans.Http.Metadata;
using Orleans.Runtime;

namespace Orleans.Http.Host
{
    public class GrainMetadataCollection
    {
        public GrainMetadataCollection(IApplicationPartManager appParts)
        {
            var assemblyParts = appParts.ApplicationParts.OfType<AssemblyPart>().ToList();
            var assemblies = new List<Assembly> { typeof(IManagementGrain).Assembly };
            assemblies.AddRange(assemblyParts.Select(p => p.Assembly));

            this.Grains = GrainDescriptionGenerator.GetGrainDescriptions(assemblies);
            this.Dispatcher = DispatcherGenerator.GetDispatcher(Grains.Values.ToList(), out var source);
            this.DispatcherSource = source;
        }

        public string DispatcherSource { get; }

        public IMethodCallDispatcher Dispatcher { get; }

        public Dictionary<string, GrainDescription> Grains { get; }
    }
}