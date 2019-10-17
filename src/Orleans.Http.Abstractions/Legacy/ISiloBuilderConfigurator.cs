using Orleans.Hosting;

namespace Orleans.Http.Abstractions
{
    public interface ISiloBuilderConfigurator
    {
        void Configure(ISiloHostBuilder siloBuilder);
    }
}
