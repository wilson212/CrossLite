﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using static CrossLite.SQLiteContext;

namespace CrossLite.CodeFirst
{
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
            where TEntity : class
        {
            // Get our table mapping
            Type entityType = typeof(TEntity);
            TableMapping table = EntityCache.GetTableMap(entityType);

            // Column defined foreign keys
            List<AttributeInfo> withFKs = new List<AttributeInfo>();

            // -----------------------------------------
            // Begin the SQL generation
            // -----------------------------------------
            StringBuilder sql = new StringBuilder("CREATE ");
            sql.AppendIf(flags.HasFlag(TableCreationOptions.Temporary), "TEMP ");
            sql.Append("TABLE ");
            sql.AppendIf(flags.HasFlag(TableCreationOptions.IfNotExists), "IF NOT EXISTS ");
            sql.AppendLine($"{Escape(table.TableName)} (");

            // -----------------------------------------
            // Append attributes
            // -----------------------------------------
            foreach (var colData in table.Columns)
            {
                // Get attribute data
                AttributeInfo info = colData.Value;
                Type propertyType = info.Property.PropertyType;
                SQLiteDataType pSqlType = GetSQLiteType(propertyType);

                // Start appending column definition SQL
                sql.Append($"\t{Escape(colData.Key)} {pSqlType}");

                // Primary Key and Unique column definition
                if (info.AutoIncrement || (table.HasPrimaryKey && info.PrimaryKey))
                {
                    sql.AppendIf(table.HasPrimaryKey && info.PrimaryKey, $" PRIMARY KEY");
                    sql.AppendIf(info.AutoIncrement && pSqlType == SQLiteDataType.INTEGER, " AUTOINCREMENT");
                }
                else if (info.Unique)
                {
                    // Unique column definition
                    sql.Append(" UNIQUE");
                }

                // Collation
                sql.AppendIf(
                    info.Collation != Collation.Default && pSqlType == SQLiteDataType.TEXT,
                    " COLLATE " + info.Collation.ToString().ToUpperInvariant()
                );

                // Nullable definition
                bool canBeNull = !propertyType.IsValueType || (Nullable.GetUnderlyingType(propertyType) != null);
                if (info.HasRequiredAttribute || (!info.PrimaryKey && !canBeNull))
                    sql.Append(" NOT NULL");

                // Default value
                if (info.DefaultValue != null)
                {
                    // Do we need to quote this?
                    SQLiteDataType type = GetSQLiteType(info.DefaultValue.GetType());
                    if (type == SQLiteDataType.INTEGER || type == SQLiteDataType.REAL)
                        sql.Append($" DEFAULT {info.DefaultValue}");
                    else
                        sql.Append($" DEFAULT \"{info.DefaultValue}\"");
                }

                // Add last comma
                sql.AppendLine(",");

                // For later use
                if (info.ForeignKey != null)
                    withFKs.Add(info);
            }

            // -----------------------------------------
            // Composite Keys
            // -----------------------------------------
            string[] keys = table.CompositeKeys; // Linq query; Perform once here
            if (!table.HasPrimaryKey && keys.Length > 0)
            {
                sql.Append($"\tPRIMARY KEY(");
                sql.Append(String.Join(", ", keys.Select(x => Escape(x))));
                sql.AppendLine("),");
            }

            // -----------------------------------------
            // Composite Unique Constraints
            // -----------------------------------------
            foreach (var cu in table.UniqueConstraints)
            {
                sql.Append($"\tUNIQUE(");
                sql.Append(String.Join(", ", cu.Attributes.Select(x => Escape(x))));
                sql.AppendLine("),");
            }

            // -----------------------------------------
            // Foreign Keys
            // -----------------------------------------
            foreach (ForeignKeyInfo info in table.ForeignKeys)
            {
                // Primary table attributes
                ForeignKeyAttribute fk = info.ForeignKey;
                string attrs1 = String.Join(", ", fk.Attributes.Select(x => Escape(x)));
                string attrs2 = String.Join(", ", info.InverseKey.Attributes.Select(x => Escape(x)));

                // Build sql command
                TableMapping map = EntityCache.GetTableMap(info.ParentEntityType);
                sql.Append($"\tFOREIGN KEY({Escape(attrs1)}) REFERENCES {Escape(map.TableName)}({attrs2})");

                // Add integrety options
                sql.AppendIf(fk.OnUpdate != ReferentialIntegrity.NoAction, $" ON UPDATE {ToSQLite(fk.OnUpdate)}");
                sql.AppendIf(fk.OnDelete != ReferentialIntegrity.NoAction, $" ON DELETE {ToSQLite(fk.OnDelete)}");

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
            using (SQLiteCommand command = context.CreateCommand(sqlLine))
            {
                command.ExecuteNonQuery();
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
            string sql = $"DROP TABLE IF EXISTS {Escape(table.TableName)}";
            using (SQLiteCommand command = context.CreateCommand(sql))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}