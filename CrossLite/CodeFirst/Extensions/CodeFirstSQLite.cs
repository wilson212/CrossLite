using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CrossLite.SQLiteContext;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Provides extension methods for creating and managing SQLite tables using a code-first approach.
    /// </summary>
    /// <remarks>This static class includes methods to create and drop tables in an SQLite database based on
    /// entity types. The table structure is generated dynamically using attributes defined on the entity's
    /// properties.</remarks>
    public static class CodeFirstSQLite
    {
        /// <summary>
        /// By passing an Entity type, this method will use the Attribute's
        /// attached to each of the entities properties to generate an 
        /// SQL command, that will create a table on the database.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="flags">Additional flags for SQL generation</param>
        public static void CreateTable<TEntity>(this SQLiteContext context, TableCreationOptions flags = TableCreationOptions.None)
            where TEntity : EntityBase
        {
            // Get our table mapping
            Type entityType = typeof(TEntity);
            TableMapping table = EntityCache.GetTableMap(entityType);

            // Column defined foreign keys and/or indexes
            List<AttributeInfo> withFKs = [];
            List<AttributeInfo> indexed = [];

            // -----------------------------------------
            // Begin the SQL generation
            // -----------------------------------------
            StringBuilder sql = new StringBuilder("CREATE ");
            sql.AppendIf(flags.HasFlag(TableCreationOptions.Temporary), "TEMP ");
            sql.Append("TABLE ");
            sql.AppendIf(flags.HasFlag(TableCreationOptions.IfNotExists), "IF NOT EXISTS ");
            sql.AppendLine($"{context.QuoteIdentifier(table.TableName)} (");

            // -----------------------------------------
            // Append attributes
            // -----------------------------------------
            foreach (var colData in table.DatabaseColumns)
            {
                // Get attribute data
                AttributeInfo info = colData.Value;
                bool isNullable = info.IsNullable;
                Type propertyType = Nullable.GetUnderlyingType(info.Property.PropertyType) ?? info.Property.PropertyType;
                SQLiteDataType pSqlType = GetSQLiteType(propertyType);

                // Start appending column definition SQL
                sql.Append($"\t{context.QuoteIdentifier(colData.Key)} {pSqlType}");

                // Primary Key and IsUnique column definition
                if (info.IsAutoIncrement || (table.HasRowIdAlias && info.IsPrimaryKey))
                {
                    sql.AppendIf(table.HasRowIdAlias && info.IsPrimaryKey, $" PRIMARY KEY");
                    sql.AppendIf(info.IsAutoIncrement && pSqlType == SQLiteDataType.INTEGER, " AUTOINCREMENT");
                }
                else if (info.IsUnique)
                {
                    // IsUnique column definition
                    sql.Append(" UNIQUE");
                }

                // Collation
                sql.AppendIf(
                    info.Collation != Collation.Default && pSqlType == SQLiteDataType.TEXT,
                    " COLLATE " + info.Collation.ToString().ToUpperInvariant()
                );

                // Nullable definition
                if (info.HasRequiredAttribute || (!info.IsPrimaryKey && !isNullable))
                    sql.Append(" NOT NULL");

                // Default value
                if (info.DefaultValue != null)
                {
                    sql.Append($" DEFAULT ");

                    // Do we need to quote this?
                    SQLiteDataType type = info.DefaultValue.SQLiteDataType;
                    if (type == SQLiteDataType.NULL)
                    {
                        sql.Append("NULL");
                    }
                    else if (type == SQLiteDataType.INTEGER && info.DefaultValue.Value is Boolean)
                    {
                        // Convert bools to integers
                        int val = ((bool)info.DefaultValue.Value) ? 1 : 0;
                        sql.Append($"{val}");
                    }
                    else if (info.DefaultValue.Quote)
                    {
                        sql.Append($"\"{info.DefaultValue.Value}\"");
                    }
                    else
                    {
                        sql.Append($"{info.DefaultValue.Value}");
                    }
                }

                // Add last comma
                sql.AppendLine(",");

                // For later use
                if (info.ForeignKey != null)
                    withFKs.Add(info);

                if (info.IsIndexed)
                    indexed.Add(info);
            }

            // -----------------------------------------
            // Composite Keys
            // -----------------------------------------
            string[] keys = table.PrimaryKeys.ToArray();
            if (!table.HasRowIdAlias && keys.Length > 0)
            {
                sql.Append($"\tPRIMARY KEY(");
                sql.Append(String.Join(", ", keys.Select(context.QuoteIdentifier)));
                sql.AppendLine("),");
            }

            // -----------------------------------------
            // Composite IsUnique Constraints
            // -----------------------------------------
            foreach (var cu in table.UniqueConstraints)
            {
                sql.Append($"\tUNIQUE(");
                sql.Append(String.Join(", ", cu.Attributes.Select(context.QuoteIdentifier)));
                sql.AppendLine("),");
            }

            // -----------------------------------------
            // Foreign Keys
            // -----------------------------------------
            foreach (ForeignKeyConstraint info in table.ForeignKeys)
            {
                // Primary table attributes
                ForeignKeyAttribute fk = info.ForeignKey;
                string attrs1 = String.Join(", ", fk.Attributes.Select(context.QuoteIdentifier));
                string attrs2 = String.Join(", ", info.Reference.Attributes.Select(context.QuoteIdentifier));

                // Build sql command
                TableMapping map = EntityCache.GetTableMap(info.ParentEntityType);
                sql.Append('\t');
                sql.Append($"FOREIGN KEY({context.QuoteIdentifier(attrs1)}) ");
                sql.Append($"REFERENCES {context.QuoteIdentifier(map.TableName)}({attrs2})");

                // Add integrety options
                sql.AppendIf(info.Reference.OnUpdate != ReferentialIntegrity.NoAction, $" ON UPDATE {ToSQLite(info.Reference.OnUpdate)}");
                sql.AppendIf(info.Reference.OnDelete != ReferentialIntegrity.NoAction, $" ON DELETE {ToSQLite(info.Reference.OnDelete)}");

                // Finish the line
                sql.AppendLine(",");
            }

            // -----------------------------------------
            // SQL wrap up
            // -----------------------------------------
            string sqlLine = String.Concat(
                sql.ToString().TrimEnd(new char[] { '\r', '\n', ',' }),
                Environment.NewLine,
                ")"
            );

            // Without row id?
            if (table.WithoutRowID)
                sqlLine += " WITHOUT ROWID;";

            // -----------------------------------------
            // Execute the command on the database
            // -----------------------------------------
            using (SqliteCommand command = context.CreateCommand(sqlLine))
            {
                command.ExecuteNonQuery();
            }

            // -----------------------------------------
            // Create Indexes
            // -----------------------------------------
            int i = 0;
            foreach (var index in table.CompositeIndexes)
            {
                // Reuse open string builder
                sql.Clear();

                // Begin
                sql.AppendIf(index.Unique, "CREATE UNIQUE INDEX ", "CREATE INDEX ");
                sql.AppendIf(String.IsNullOrEmpty(index.Name), $"idx_{table.TableName}_{i}", context.QuoteIdentifier(index.Name));
                sql.Append($" ON {context.QuoteIdentifier(table.TableName)}(");

                // Append columns
                sql.Append(String.Join(", ", index.Columns.Select(context.QuoteIdentifier)));
                sql.Append(')');

                // Execute
                using (SqliteCommand command = context.CreateCommand(sql.ToString()))
                {
                    command.ExecuteNonQuery();
                }

                // Increment counter
                i++;
            }

            foreach (var column in indexed)
            {
                // Reuse open string builder
                sql.Clear();

                // Begin
                sql.AppendIf(column.IsUnique, "CREATE UNIQUE INDEX ", "CREATE INDEX ");
                sql.Append($"idx_{table.TableName}_{column.ColumnName}");
                sql.Append($" ON {context.QuoteIdentifier(table.TableName)}(");
                sql.Append(context.QuoteIdentifier(column.ColumnName));
                sql.Append(')');

                // Execute
                using (SqliteCommand command = context.CreateCommand(sql.ToString()))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Drops the specified table Entity from the database if it exists
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        public static void DropTable<TEntity>(this SQLiteContext context) where TEntity : class
        {
            // Get our table mapping
            Type entityType = typeof(TEntity);
            TableMapping table = EntityCache.GetTableMap(entityType);

            // Build the SQL query and perform the deletion
            string sql = $"DROP TABLE IF EXISTS {context.QuoteIdentifier(table.TableName)}";
            using (SqliteCommand command = context.CreateCommand(sql))
            {
                command.Transaction = context.Transaction;
                command.ExecuteNonQuery();
            }
        }
    }
}
