using System;

namespace Orleans.Http.Abstractions
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
    public class HttpVisibleAttribute : Attribute
    {
        public HttpVisibleAttribute(bool visible)
        {
            this.Visible = visible;
        }

        public bool? Visible { get; set; }
    }
}
