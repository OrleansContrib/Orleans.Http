using System;

namespace Orleans.Http.Abstractions
{
    public abstract class GrainRouteAttributeBase : Attribute
    {
        public string Pattern { get; private set; }
        public string Name { get; private set; }
        public string RouteGrainProviderPolicy { get; private set; }

        protected GrainRouteAttributeBase(string pattern = "", string name = "", string routeGrainProviderPolicy = "")
        {
            this.Pattern = pattern;
            this.Name = name;
            this.RouteGrainProviderPolicy = routeGrainProviderPolicy;
        }
    }

    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
    public class RouteAttribute : GrainRouteAttributeBase
    {
        public RouteAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    public abstract class MethodAttribute : GrainRouteAttributeBase
    {
        public abstract string Method { get; }

        protected MethodAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpGetAttribute : MethodAttribute
    {
        public override string Method { get => "GET"; }
        public HttpGetAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpPostAttribute : MethodAttribute
    {
        public override string Method { get => "POST"; }
        public HttpPostAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpPutAttribute : MethodAttribute
    {
        public override string Method { get => "PUT"; }
        public HttpPutAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpDeleteAttribute : MethodAttribute
    {
        public override string Method { get => "DELETE"; }
        public HttpDeleteAttribute(string pattern = "", string name = "", string routeGrainProviderPolicy = "") : base(pattern, name, routeGrainProviderPolicy) { }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FromBodyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FromQueryAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AuthorizeAttribute : Attribute
    {
        public AuthorizeAttribute() { }
        public AuthorizeAttribute(string policy) { this.Policy = policy; }
        public string AuthenticationSchemes { get; set; }
        public string Policy { get; set; }
        public string Roles { get; set; }
    }
}