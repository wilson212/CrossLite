using System;

namespace CrossLite.CodeFirst
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CompositeIndexAttribute : Attribute
    {
        public string[] Properties { get; set; }

        public string Name { get; set; }

        public bool Unique { get; set; }

        public CompositeIndexAttribute(params string[] properties)
        {
            this.Properties = properties;
        }
    }
}
