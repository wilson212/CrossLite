using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a Without Row ID attribute. Only used in CodeFirst table 
    /// creation <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class WithoutRowIdAttribute : Attribute
    {

    }
}
