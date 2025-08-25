using System;

namespace CrossLite.CodeFirst
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CompositeIndexAttribute : Attribute
    {
        public string[] Columns { get; set; }

        public string Name { get; set; }

        public bool Unique { get; set; }

        public CompositeIndexAttribute(params string[] columns)
        {
            this.Columns = columns;
        }
    }
}
