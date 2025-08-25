using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a SQLite database table. Only used in CodeFirst table 
    /// creation <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// If specified, this is the table name this Entity represents
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or Sets whether the "WITHOUT ROWID" command is used
        /// when creating a table using Code First 
        /// (see <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>)
        /// </summary>
        public bool WithoutRowID { get; set; }

        /// <summary>
        /// Gets or sets whether the <see cref="ForeignKeyLoader{TEntity}"/>
        /// relationships are built on instances of this table, when fetching
        /// or creating new Entities.
        /// </summary>
        /// <remarks>
        /// If true, Entities with a large number (3+) foreign keys will notice a performance
        /// hit when being inserted into the DbSet, or retireved from the SQLiteContext.
        /// </remarks>
        public bool BuildInstanceRelationships { get; set; }

        /// <summary>
        /// Creates a new SQLite Entity to table relationship
        /// </summary>
        /// <param name="Name">
        /// If not null, this is the table name that this Entity will represent
        /// </param>
        /// <param name="WithoutRowID">
        /// Indicates whether this table has the 'WithoutRowId' clause.
        /// </param>
        /// <param name="BuildRelationships">
        /// If true, the <see cref="ForeignKeyLoader{TEntity}"/> attributes will be filled after insertion,
        /// otherwise they are left null. There is a slight performance hit when true.
        /// </param>
        public TableAttribute(string Name = null, bool WithoutRowID = false, bool BuildRelationships = true)
        {
            this.Name = Name;
            this.WithoutRowID = WithoutRowID;
            this.BuildInstanceRelationships = BuildRelationships;
        }
    }
}
