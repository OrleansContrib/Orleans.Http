using System;

namespace Orleans.Http.Abstractions
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpGrainMethodAttribute : Attribute
    {
        public HttpGrainMethodAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}