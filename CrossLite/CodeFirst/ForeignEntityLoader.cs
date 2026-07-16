using CrossLite.QueryBuilder;
using System;
using System.Linq;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Provides functionality to manage and retrieve parent entities associated with a child entity in a relational
    /// database context. This class is designed to handle foreign key relationships between entities and facilitates
    /// operations such as fetching the parent entity from the database.
    /// </summary>
    /// <remarks>
    /// This class requires an active database context. If you need to access navigation properties after the context
    /// is disposed of, use .Include() to eagerly load them within the using block.
    /// </remarks>
    /// <typeparam name="TParentEntity">The type of the parent entity. Must inherit from <see cref="EntityBase"/>.</typeparam>
    /// <typeparam name="TChildEntity">The type of the child entity. Must inherit from <see cref="EntityBase"/> and have a parameterless constructor.</typeparam>
    internal class ForeignEntityLoader<TParentEntity, TChildEntity>  : IEntityFetcher
        where TParentEntity : EntityBase, new()
        where TChildEntity : EntityBase, new()
    {
        /// <summary>
        /// Gets or sets the SQLite database context used for interacting with the database.
        /// </summary>
        private SQLiteContext Context { get; set; }

        /// <summary>
        /// Gets or sets the child entity associated with the current object.
        /// </summary>
        private object ChildEntity { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ForeignKeyConstraint"/> that defines the relationship between parent and child
        /// tables.
        /// </summary>
        private ForeignKeyConstraint Constraint { get; set; }

        /// <summary>
        /// Gets or sets the child table mapping associated with this instance.
        /// </summary>
        private TableMapping ChildTable { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="SelectWhereStatement"/> that defines the selection and filtering criteria for
        /// the query.
        /// </summary>
        private SelectWhereStatement Statement { get; set; }

        /// <summary>
        /// Creates a new Instance of <see cref="ForeignEntityLoader{TParentEntity, TChildEntity}"/>
        /// </summary>
        /// <param name="childEntity">The Child Entity object WITH the Foreign Key restraint</param>
        /// <param name="constraint">The foreign key constraint defining the relationship</param>
        /// <param name="context">An open SQLiteContext that hosts these entities</param>
        public ForeignEntityLoader(TChildEntity childEntity, ForeignKeyConstraint constraint, SQLiteContext context)
        {
            // Set properties
            Context = context;
            ChildEntity = childEntity;

            // Define entity types
            var childType = typeof(TChildEntity);
            var parentType = typeof(TParentEntity);

            // Grab mapping and foreign info from child entity
            ChildTable = TableCache.GetTableMap(childType);
            Constraint = constraint;

            // Make sure the user set their code up correctly
            if (Constraint == null)
            {
                throw new EntityException(
                    $"Entity \"{childType.Name}\" does not contain a ForeignKey attribute for {parentType.Name}"
                );
            }

            // Refresh where statement
            Refresh();
        }

        /// <summary>
        /// Refreshes the internal SQL statement to reflect any property value changes
        /// within the child entity. This method should be called whenever a foreign key
        /// value changes.
        /// </summary>
        public void Refresh()
        {
            // Create a new WHERE Statement
            Statement = new SelectWhereStatement();

            // Fill up the WhereStatement with joining keys specific to this Child
            // entities instance
            for (int i = 0; i < Constraint.ForeignKey.PropertyNames.Length; i++)
            {
                // Grab attribute names
                string childPropName = Constraint.ForeignKey.PropertyNames[i];
                string parentPropName = Constraint.Reference.PropertyNames[i];

                // Get the value of the child attribute on this Entity instance
                var info = ChildTable.GetAttributeByPropertyName(childPropName);
                var val = info.GetValue(ChildEntity);

                // Add the key => value to the where statement
                var parentTable = TableCache.GetTableMap(typeof(TParentEntity));
                var parentColName = parentTable.GetAttributeByPropertyName(parentPropName).ColumnName;
                Statement.And(parentColName, Comparison.Equals, val);
            }
        }

        /// <summary>
        /// Gets the current Parent Entity value from the database,
        /// that this Child Entity instance is bound to.
        /// </summary>
        /// <remarks>
        /// This method requires an active database context. If the context has been disposed,
        /// an InvalidOperationException will be thrown. To access navigation properties after
        /// the context is disposed of, use .Include() to eagerly load them.
        /// </remarks>
        /// <returns>The parent entity, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the database context is not connected.</exception>
        public object Fetch()
        {
            // Fail fast if the context is not connected
            if (Context == null || !Context.IsConnected())
            {
                throw new InvalidOperationException(
                    $"Cannot lazy-load navigation property '{typeof(TParentEntity).Name}' because the database context is not connected. " +
                    $"To access navigation properties outside a 'using' block, use .Include() to eagerly load them:\n\n" +
                    $"Example:\n" +
                    $"  var entities = context.Select<{typeof(TChildEntity).Name}>(x => ...)\n" +
                    $"      .Include(x => x.{typeof(TParentEntity).Name})\n" +
                    $"      .ToList();"
                );
            }

            var objType = typeof(TParentEntity);
            var table = TableCache.GetTableMap(objType);

            // Build the SQL query using your builder
            var builder = new SelectQueryBuilder(Context);
            builder.From(table.TableName).SelectAll().Take(1);
            builder.WhereStatement = Statement;

            using var command = builder.BuildCommand();
            using var reader = command.ExecuteReader();
            
            if (reader.HasRows)
            {
                reader.Read();

                // 1. Resolve PK ordinals once for the identity check
                int[] pkOrdinals = null;
                if (Context.UseIdentityMapping && table.PrimaryKeys.Count > 0)
                {
                    pkOrdinals = table.PrimaryKeys
                        .Select(pk => reader.GetOrdinal(pk.ColumnName))
                        .ToArray();
                }

                // 2. Return via ConvertToEntity to ensure the Identity Map is checked
                return Context.ConvertToEntity<TParentEntity>(table, reader, pkOrdinals);
            }
            
            return null;
        }
    }
}
