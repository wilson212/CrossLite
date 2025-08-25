using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a IsUnique attribute constraint of values. Only used in CodeFirst table 
    /// creation <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CompositeUniqueAttribute : Attribute
    {
        public string[] Attributes { get; protected set; }

        public CompositeUniqueAttribute(params string[] attributes)
        {
            Attributes = attributes;
        }
    }
}
