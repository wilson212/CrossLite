using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Specifies a composite unique constraint for a class. This attribute is used
    /// to designate which properties of a class must be treated as a composite
    /// unique key, ensuring that the combination of these properties is unique
    /// across all rows in the database.
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
