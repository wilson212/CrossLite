using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Linq;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Provides functionality to manage and retrieve parent entities associated with a child entity in a relational
    /// database context. This class is designed to handle foreign key relationships between entities and facilitates
    /// operations such as fetching the parent entity from the database.
    /// </summary>
    /// <remarks>This class is intended for use in scenarios where entities are related through foreign key
    /// constraints. It provides methods to refresh the internal SQL statement based on the child entity's foreign key
    /// values and to fetch the parent entity from the database.</remarks>
    /// <typeparam name="TParentEntity">The type of the parent entity. Must inherit from <see cref="EntityBase"/>.</typeparam>
    /// <typeparam name="TChildEntity">The type of the child entity. Must inherit from <see cref="EntityBase"/> and have a parameterless constructor.</typeparam>
    internal class ForeignEntityLoader<TParentEntity, TChildEntity>  : IEntityFetcher
        where TParentEntity : EntityBase, new()
        where TChildEntity : EntityBase, new()
    {
        /// <summary>
        /// Gets or sets the SQLite database context used for interacting with the database.
        /// </summary>
        protected SQLiteContext Context { get; set; }

        /// <summary>
        /// Gets or sets the child entity associated with the current object.
        /// </summary>
        protected object ChildEntity { get; set; }

        /// <summary>
        /// Gets or sets the connection string used to establish a connection to the database.
        /// </summary>
        protected string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ForeignKeyConstraint"/> that defines the relationship between parent and child
        /// tables.
        /// </summary>
        protected ForeignKeyConstraint Constraint { get; set; }

        /// <summary>
        /// Gets or sets the child table mapping associated with this instance.
        /// </summary>
        /// <remarks>The child table mapping is typically used in scenarios involving hierarchical or
        /// relational data structures. Ensure that the value assigned to this property is not null and represents a
        /// valid table mapping.</remarks>
        protected TableMapping ChildTable { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="SelectWhereStatement"/> that defines the selection and filtering criteria for
        /// the query.
        /// </summary>
        protected SelectWhereStatement Statement { get; set; }

        /// <summary>
        /// Creates a new Instance of <see cref="ForeignEntityLoader{TParentEntity, TChildEntity}"/>
        /// </summary>
        /// <param name="childEntity">The Child Entity object WITH the Foreign Key restraint</param>
        /// <param name="childPropertry">The property from the child property, that hosts the foreign key object</param>
        /// <param name="context">An open SQLiteContext that hosts these entities</param>
        public ForeignEntityLoader(TChildEntity childEntity, ForeignKeyConstraint constraint, SQLiteContext context)
        {
            // Set properties
            Context = context;
            ChildEntity = childEntity;
            ConnectionString = context.ConnectionString;

            // Define entity types
            Type childType = typeof(TChildEntity);
            Type parentType = typeof(TParentEntity);

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

            // Refresh where statment
            Refresh();
        }

        /// <summary>
        /// Refreshes the internal SQL statement to reflect any property value changes
        /// within the child entity. This method should be called whenever a foreign key
        /// value changes.
        /// </summary>
        public void Refresh()
        {
            // Create new WHERE Statement
            Statement = new SelectWhereStatement();

            // Fill up the WhereStatement with joining keys specific to this Child
            // entities instance
            for (int i = 0; i < Constraint.ForeignKey.PropertyNames.Length; i++)
            {
                // Grab attribute names
                string childPropName = Constraint.ForeignKey.PropertyNames[i];
                string parentPropName = Constraint.Reference.PropertyNames[i];

                // Get the value of the child attribute on this Entity instance
                AttributeInfo info = ChildTable.GetAttributeByPropertyName(childPropName);
                object val = info.GetValue(ChildEntity);

                // Add the key => value to the where statement
                var parentTable = TableCache.GetTableMap(typeof(TParentEntity));
                string parentColName = parentTable.GetAttributeByPropertyName(parentPropName).ColumnName;
                Statement.And(parentColName, Comparison.Equals, val);
            }
        }

        /// <summary>
        /// Gets the current Parent Entity value from the database,
        /// that this Child Entity instance is bound to.
        /// </summary>
        /// <returns></returns>
        public object Fetch()
        {
            Type objType = typeof(TParentEntity);
            TableMapping table = TableCache.GetTableMap(objType);

            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            // Build the SQL query using your builder
            SelectQueryBuilder builder = new SelectQueryBuilder(context);
            builder.From(table.TableName).SelectAll().Take(1);
            builder.WhereStatement = Statement;

            try
            {
                using (SqliteCommand command = builder.BuildCommand())
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        reader.Read();

                        // 1. Resolve PK ordinals once for the identity check
                        int[] pkOrdinals = null;
                        if (context.UseIdentityMapping && table.PrimaryKeys.Count > 0)
                        {
                            pkOrdinals = table.PrimaryKeys
                                .Select(pk => reader.GetOrdinal(pk.ColumnName))
                                .ToArray();
                        }

                        // 2. Return via ConvertToEntity to ensure the Identity Map is checked
                        return context.ConvertToEntity<TParentEntity>(table, reader, pkOrdinals);
                    }
                    return null;
                }
            }
            finally
            {
                // Ensure the context is disposed only if we opened it locally
                if (!wasOpen)
                {
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Lazily initializes and retrieves a database context for the current operation.
        /// </summary>
        /// <param name="wasOpen">Indicates if the context was already open.</param>
        /// <returns>The active SQLiteContext.</returns>
        protected SQLiteContext LazyLoad(ref bool wasOpen)
        {
            SQLiteContext context = null;
            wasOpen = false;

            // Use the existing context if it's connected
            if (Context != null && Context.IsConnected())
            {
                wasOpen = true;
                context = Context;
            }
            else
            {
                // Otherwise, open a new connection for this fetch
                context = new SQLiteContext(ConnectionString);
                context.Connect();
            }

            return context;
        }
    }
}
