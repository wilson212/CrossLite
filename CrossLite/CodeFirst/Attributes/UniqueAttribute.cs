using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a IsUnique attribute value. Only used in CodeFirst table 
    /// creation <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueAttribute : Attribute
    {
        public UniqueAttribute()
        {

        }
    }
}
