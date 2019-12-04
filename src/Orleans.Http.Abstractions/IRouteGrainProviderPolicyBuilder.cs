namespace Orleans.Http.Abstractions
{
    public interface IRouteGrainProviderPolicyBuilder
    {
        IRouteGrainProviderPolicyBuilder RegisterRouteGrainProvider<T>(string policyName) where T : IRouteGrainProvider;
        IRouteGrainProviderPolicyBuilder SetDefaultRouteGrainProviderPolicy(string policyName);
    }
}
