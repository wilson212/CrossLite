using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a default value for an Attribute. Only used in CodeFirst 
    /// table creation: <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultAttribute : Attribute
    {
        /// <summary>
        /// Gets or Sets the default value, if any
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets the <see cref="SQLiteDataType"/> of this default value
        /// </summary>
        public SQLiteDataType SQLiteDataType { get; protected set; }

        /// <summary>
        /// Gets or Sets whether to Quote this default value in SQL code First statements
        /// </summary>
        public bool Quote { get; set; } = true;

        public DefaultAttribute(object val)
        {
            this.Value = val;
            if (val == null)
            {
                this.SQLiteDataType = SQLiteDataType.NULL;
                this.Quote = false; // NULL does not need quoting
                return;
            }
            this.SQLiteDataType = SQLiteContext.GetSQLiteType(val.GetType());
            this.Quote = (SQLiteDataType != SQLiteDataType.INTEGER && SQLiteDataType != SQLiteDataType.REAL);
        }
    }
}
