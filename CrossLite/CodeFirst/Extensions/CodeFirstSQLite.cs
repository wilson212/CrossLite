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
            TableMapping table = TableCache.GetTableMap(entityType);

            // Column defined foreign keys and/or indexes
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
                if (info.HasRequiredAttribute || !isNullable)
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

                if (info.IsIndexed)
                    indexed.Add(info);
            }

            // -----------------------------------------
            // Composite Keys
            // -----------------------------------------
            string[] keys = table.PrimaryKeys.Select(x => context.QuoteIdentifier(x.ColumnName)).ToArray();
            if (!table.HasRowIdAlias && keys.Length > 0)
            {
                sql.Append($"\tPRIMARY KEY(");
                sql.Append(String.Join(", ", keys));
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
                string attrs1 = String.Join(", ", info.GetForeignKeyColumnNames().Select(context.QuoteIdentifier));
                string attrs2 = String.Join(", ", info.GetReferenceColumnNames().Select(context.QuoteIdentifier));

                // Build sql command
                TableMapping map = TableCache.GetTableMap(info.ParentEntityType);
                sql.Append('\t');
                sql.Append($"FOREIGN KEY({attrs1}) ");
                sql.Append($"REFERENCES {context.QuoteIdentifier(map.TableName)}({attrs2})");

                // Add integrety options
                sql.AppendIf(info.Reference.OnUpdate != ReferentialAction.NoAction, $" ON UPDATE {ToSQLite(info.Reference.OnUpdate)}");
                sql.AppendIf(info.Reference.OnDelete != ReferentialAction.NoAction, $" ON DELETE {ToSQLite(info.Reference.OnDelete)}");

                // Finish the line
                sql.AppendLine(",");
            }

            // -----------------------------------------
            // SQL wrap up
            // -----------------------------------------
            string sqlLine = String.Concat(
                sql.ToString().TrimEnd(['\r', '\n', ',']),
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
                sql.Append(String.Join(", ", index.Properties.Select(context.QuoteIdentifier)));
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
        public static void DropTable<TEntity>(this SQLiteContext context) where TEntity : EntityBase
        {
            // Get our table mapping
            Type entityType = typeof(TEntity);
            TableMapping table = TableCache.GetTableMap(entityType);

            // Build the SQL query and perform the deletion
            string sql = $"DROP TABLE IF EXISTS {context.QuoteIdentifier(table.TableName)}";
            using (SqliteCommand command = context.CreateCommand(sql))
            {
                command.Transaction = context.Transaction;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Checks the database for the existing table and adds any missing columns defined in the C# Entity.
        /// </summary>
        /// <remarks>
        /// Developers should explicitly use [Default("O+")] attribute when adding new non-nullable number based columns.
        /// </remarks>
        public static void MigrateTable<TEntity>(this SQLiteContext context) where TEntity : EntityBase
        {
            Type entityType = typeof(TEntity);
            TableMapping table = TableCache.GetTableMap(entityType);

            // Get existing columns from the database
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string pragmaSql = $"PRAGMA table_info({context.QuoteIdentifier(table.TableName)});";

            using (SqliteCommand command = context.CreateCommand(pragmaSql))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // The 'name' column is at index 1 in the PRAGMA table_info result
                    existingColumns.Add(reader.GetString(1));
                }
            }

            // If the table doesn't exist at all, just create it and exit
            if (existingColumns.Count == 0)
            {
                context.CreateTable<TEntity>(TableCreationOptions.IfNotExists);
                return;
            }

            // Find missing columns by comparing the mapping to the DB
            foreach (var colData in table.DatabaseColumns)
            {
                string columnName = colData.Key;
                AttributeInfo info = colData.Value;

                if (!existingColumns.Contains(columnName))
                {
                    // Build the ALTER TABLE ADD COLUMN command
                    StringBuilder sql = new StringBuilder();
                    sql.Append($"ALTER TABLE {context.QuoteIdentifier(table.TableName)} ADD COLUMN ");

                    // Get types
                    Type propertyType = Nullable.GetUnderlyingType(info.Property.PropertyType) ?? info.Property.PropertyType;
                    SQLiteDataType pSqlType = GetSQLiteType(propertyType);

                    sql.Append($"{context.QuoteIdentifier(columnName)} {pSqlType}");

                    // Collation
                    if (info.Collation != Collation.Default && pSqlType == SQLiteDataType.TEXT)
                        sql.Append(" COLLATE " + info.Collation.ToString().ToUpperInvariant());

                    // Nullable definition
                    bool isNullable = info.IsNullable;
                    if (info.HasRequiredAttribute || !isNullable)
                    {
                        sql.Append(" NOT NULL");

                        // CRITICAL SQLITE QUIRK: If you add a NOT NULL column to an existing table, 
                        // SQLite REQUIRES a DEFAULT value so it knows what to put in the existing rows.
                        if (info.DefaultValue == null)
                        {
                            if (pSqlType == SQLiteDataType.INTEGER || pSqlType == SQLiteDataType.REAL)
                                sql.Append(" DEFAULT 0");
                            else if (pSqlType == SQLiteDataType.TEXT)
                                sql.Append(" DEFAULT ''");
                        }
                    }

                    // Default value
                    if (info.DefaultValue != null)
                    {
                        sql.Append(" DEFAULT ");
                        SQLiteDataType type = info.DefaultValue.SQLiteDataType;

                        if (type == SQLiteDataType.NULL) 
                            sql.Append("NULL");
                        else if (type == SQLiteDataType.INTEGER && info.DefaultValue.Value is Boolean val) 
                            sql.Append(val ? "1" : "0");
                        else if (info.DefaultValue.Quote) 
                            sql.Append($"\"{info.DefaultValue.Value}\"");
                        else 
                            sql.Append($"{info.DefaultValue.Value}");
                    }

                    // Execute the ALTER TABLE command immediately for this column
                    using (SqliteCommand alterCmd = context.CreateCommand(sql.ToString()))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// This method is used to perform a mass-migration on a table in the database.
        /// Essentially, this method renames the table, creates a new table using the same
        /// name, and copies all the data from the old table to the new.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <remarks>
        /// You only use RecreateTable<TEntity> when SQLite's ALTER TABLE limitations force you to. Specifically:
        ///  - Changing a Data Type: (e.g., changing Callsign from int to string).
        ///  - Removing a Column: SQLite does not support ALTER TABLE DROP COLUMN natively in older versions(though it was added in version 3.35.0, older runtimes might not support it).
        ///  - Adding a UNIQUE Constraint: You cannot add this to an existing column via ALTER TABLE.
        /// </remarks>
        public static void RecreateTable<TEntity>(this SQLiteContext context) where TEntity : EntityBase, new()
        {
            TableMapping table = TableCache.GetTableMap(typeof(TEntity));
            var newName = table.TableName + "_old";

            // Blindfold SQLite to prevent child tables from updating their FK pointers
            context.Execute("PRAGMA foreign_keys = OFF;");

            using var ts = context.BeginTransaction();
            try
            {
                // Rename and recreate
                context.Execute($"ALTER TABLE `{table.TableName}` RENAME TO `{newName}`");
                context.CreateTable<TEntity>();

                // Find matching columns to prevent schema crash
                var oldColumns = new List<string>();
                using (var cmd = context.CreateCommand($"PRAGMA table_info(`{newName}`);"))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) oldColumns.Add(reader.GetString(1));
                }

                var sharedColumns = oldColumns.Intersect(table.DatabaseColumns.Keys, StringComparer.OrdinalIgnoreCase).ToList();

                // Disk-to-disk transfer
                if (sharedColumns.Any())
                {
                    string cols = string.Join(", ", sharedColumns.Select(c => $"`{c}`"));
                    context.Execute($"INSERT INTO `{table.TableName}` ({cols}) SELECT {cols} FROM `{newName}`;");
                }

                // Cleanup
                context.Execute($"DROP TABLE `{newName}`");
                ts.Commit();
            }
            catch
            {
                ts.Rollback();
                throw;
            }
            finally
            {
                // Restore constraints. Child tables automatically link to the new table!
                context.Execute("PRAGMA foreign_keys = ON;");
            }
        }

        /// <summary>
        /// Checks the database for existing indexes and creates any missing ones defined in the C# Entity.
        /// </summary>
        public static void EnsureIndexes<TEntity>(this SQLiteContext context) where TEntity : EntityBase
        {
            TableMapping table = TableCache.GetTableMap(typeof(TEntity));

            // 1. Get existing indexes from the database
            var existingIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string pragmaSql = $"PRAGMA index_list({context.QuoteIdentifier(table.TableName)});";

            using (var command = context.CreateCommand(pragmaSql))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // The 'name' column is at index 1 in the PRAGMA index_list result
                    existingIndexes.Add(reader.GetString(1));
                }
            }

            // 2. Check and Create Composite Indexes
            int i = 0;
            foreach (var index in table.CompositeIndexes)
            {
                string expectedName = string.IsNullOrEmpty(index.Name) ? $"idx_{table.TableName}_{i}" : index.Name;

                if (!existingIndexes.Contains(expectedName))
                {
                    string uniquePrefix = index.Unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                    string cols = string.Join(", ", index.Properties.Select(context.QuoteIdentifier));
                    string sql = $"{uniquePrefix} {context.QuoteIdentifier(expectedName)} ON {context.QuoteIdentifier(table.TableName)}({cols});";

                    context.Execute(sql);
                }
                i++;
            }

            // 3. Check and Create Single-Column Indexes
            foreach (var colData in table.DatabaseColumns.Values.Where(c => c.IsIndexed))
            {
                string expectedName = $"idx_{table.TableName}_{colData.ColumnName}";

                if (!existingIndexes.Contains(expectedName))
                {
                    string uniquePrefix = colData.IsUnique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                    string sql = $"{uniquePrefix} {context.QuoteIdentifier(expectedName)} ON {context.QuoteIdentifier(table.TableName)}({context.QuoteIdentifier(colData.ColumnName)});";

                    context.Execute(sql);
                }
            }
        }
    }
}
