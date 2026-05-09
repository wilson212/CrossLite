using Castle.DynamicProxy;
using CrossLite.CodeFirst;
using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CrossLite
{
    /// <summary>
    /// This class represents an SQLite connection with ORM query methods.
    /// 
    /// ORM is a technique to map database objects to Object Oriented Programming 
    /// Objects to let the developer focus on programming in an Object 
    /// Oriented manner.
    /// </summary>
    public class SQLiteContext : IDisposable
    {
        /// <summary>
        /// Gets or sets the default <see cref="CrossLite.IdentifierQuoteMode"/> for queries. New instances of
        /// <see cref="SQLiteContext"/> with automatically dfefault to this value.
        /// </summary>
        public static IdentifierQuoteMode DefaultIdentifierQuoteMode { get; set; } = IdentifierQuoteMode.None;

        /// <summary>
        /// Gets or sets the default <see cref="CrossLite.IdentifierQuoteKind"/> for queries. New instances of
        /// <see cref="SQLiteContext"/> with automatically dfefault to this value.
        /// </summary>
        public static IdentifierQuoteKind DefaultIdentifierQuoteKind { get; set; } = IdentifierQuoteKind.Default;

        /// <summary>
        /// The database connection
        /// </summary>
        public SqliteConnection Connection { get; protected set; }

        /// <summary>
        /// Gets the current database transaction associated with the connection.
        /// </summary>
        public SqliteTransaction Transaction { get; protected set; }

        /// <summary>
        /// Indicates whether the disposed method was called
        /// </summary>
        protected bool IsDisposed = false;

        /// <summary>
        /// Gets or sets the <see cref="CrossLite.IdentifierQuoteMode"/> this instance will use for queries
        /// </summary>
        public IdentifierQuoteMode IdentifierQuoteMode { get; set; } = DefaultIdentifierQuoteMode;

        /// <summary>
        /// Gets or sets the <see cref="CrossLite.IdentifierQuoteKind"/> this instance will use for queries
        /// </summary>
        public IdentifierQuoteKind IdentifierQuoteKind { get; set; } = DefaultIdentifierQuoteKind;

        /// <summary>
        /// Contains the conenction string used to open this connection
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Gets the <see cref="ProxyGenerator"/> instance used to create dynamic proxy objects.
        /// </summary>
        /// <remarks>This property provides access to the underlying <see cref="ProxyGenerator"/>
        /// instance,  which can be used to generate proxy types or instances for intercepting method calls.</remarks>
        private static readonly ProxyGenerator Generator = new ProxyGenerator();

        /// <summary>
        /// 
        /// </summary>
        public bool UseIdentityMapping { get; set; } = true;
        
        /// <summary>
        /// 
        /// </summary>
        internal Dictionary<Type, Dictionary<EntityKey, EntityBase>> EntityIdentityMap { get; } = [];

        /// <summary>
        /// Creates a new connection to an SQLite Database
        /// </summary>
        /// <param name="connectionString">The Connection string to connect to this database</param>
        public SQLiteContext(string connectionString)
        {
            ConnectionString = connectionString;
            Connection = new SqliteConnection(connectionString);
            Connection.Disposed += Connection_Disposed;
        }

        /// <summary>
        /// Creates a new connection to an SQLite Database
        /// </summary>
        /// <param name="builder">The Connection string to connect to this database</param>
        public SQLiteContext(SqliteConnectionStringBuilder builder)
        {
            ConnectionString = builder.ToString();
            Connection = new SqliteConnection(ConnectionString);
            Connection.Disposed += Connection_Disposed;
        }

        private void Connection_Disposed(object sender, EventArgs e)
        {
            IsDisposed = true;
        }

        /// <summary>
        /// Disposes the DB connection
        /// </summary>
        public void Dispose()
        {
            if (Connection != null && !IsDisposed)
            {
                try
                {
                    Connection.Close();
                    Connection.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    IsDisposed = true;
                    ClearIdentityMap();
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Removes a specific entity from the identity map using its primary key(s).
        /// Values must be provided in the order defined by the PrimaryKey attribute.
        /// </summary>
        public void Detach<T>(params object[] keyValues) where T : EntityBase
        {
            var table = TableCache.GetTableMap(typeof(T));
            
            // Validate and pack using the same logic as Find()
            var packedKey = PackIdentityKey(table, keyValues);
            if (EntityIdentityMap.TryGetValue(typeof(T), out var typeMap))
            {
                typeMap.Remove(packedKey);
            }
        }

        /// <summary>
        /// Removes a specific entity from the identity map.
        /// </summary>
        /// <param name="entity"></param>
        public void Detach<T>(T entity) where T : EntityBase
        {
            var table = TableCache.GetTableMap(entity.GetType());
            
            // Extract values directly from the entity using the sorted metadata
            var values = table.PrimaryKeys.Select(pk => pk.GetValue(entity)).ToArray();
            var packedKey = PackIdentityKey(table, values);
            
            if (EntityIdentityMap.TryGetValue(typeof(T), out var typeMap))
            {
                typeMap.Remove(packedKey);
            }
        }

        /// <summary>
        /// Clears the entire identity map for all entity types.
        /// </summary>
        public void ClearIdentityMap()
        {
            EntityIdentityMap.Clear();
        }

        /// <summary>
        /// Clears all cached entities for a specific type.
        /// </summary>
        public void ClearIdentityMap(Type entityType)
        {
            if (EntityIdentityMap.TryGetValue(entityType, out var typeMap))
            {
                typeMap.Clear();
            }
        }

        #region Connection Management

        /// <summary>
        /// Opens the database connection
        /// </summary>
        public void Connect()
        {
            if (Connection.State != ConnectionState.Open)
            {
                try
                {
                    Connection.Open();
                }
                catch (Exception e)
                {
                    throw new DbConnectException("Unable to etablish database connection", e);
                }
            }
        }

        /// <summary>
        /// Closes the connection to the database
        /// </summary>
        public void Close()
        {
            try
            {
                if (Connection.State != ConnectionState.Closed)
                    Connection.Close();
            }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Indicates whether the database connection appears to be open
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            if (IsDisposed) return false;
            return Connection.State == ConnectionState.Open;
        }

        #endregion Connection Management

        #region Database Management

        /// <summary>
        /// Performs an integrity check on the database, and returns the
        /// number of issues found.
        /// </summary>
        /// <returns></returns>
        public int PerformIntegrityCheck()
        {
            // Log any integrity errors in the database
            var results = Query("PRAGMA integrity_check;").ToList();
            if (results.Count > 0 && results[0]["integrity_check"].ToString() != "ok")
            {
                //LogErrors(results, "IntegrityErrors.log");
                return results.Count;
            }

            return 0;
        }

        /// <summary>
        /// Performs a VACUUM on the database
        /// </summary>
        /// <seealso cref="https://sqlite.org/lang_vacuum.html"/>
        public void VacuumDatabase()
        {
            Execute("VACUUM;");
        }

        #endregion

        #region Execute Methods

        /// <summary>
        /// Executes a statement on the database (Update, Delete, Insert)
        /// </summary>
        /// <param name="sql">The SQL statement to be executes</param>
        /// <returns>Returns the number of rows affected by the statement</returns>
        public int Execute(string sql)
        {
            // Create the SQL Command
            using (SqliteCommand Command = this.CreateCommand(sql))
                return Command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a statement on the database (Update, Delete, Insert)
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <param name="parameters">A list of Sqlparameters</param>
        /// <returns>Returns the number of rows affected by the statement</returns>
        public int Execute(string sql, List<DbParameter> parameters)
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
            {
                // Add params
                foreach (DbParameter Param in parameters)
                    Command.Parameters.Add(Param);

                // Execute command, and dispose of the command
                return Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a statement on the database (Update, Delete, Insert)
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <param name="items">Additional parameters are parameter values for the query.
        /// The first parameter replaces @P0, second @P1 etc etc.
        /// </param>
        /// <returns>Returns the number of rows affected by the statement</returns>
        public int Execute(string sql, params object[] items)
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
            {
                // Add params
                for (int i = 0; i < items.Length; i++)
                {
                    DbParameter Param = this.CreateParameter();
                    Param.ParameterName = "@P" + i;
                    Param.Value = items[i];
                    Command.Parameters.Add(Param);
                }

                // Execute command, and dispose of the command
                return Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <returns></returns>
        public object ExecuteScalar(string sql)
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
                return Command.ExecuteScalar();
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <param name="parameters">A list of Sqlparameters</param>
        /// <returns></returns>
        public object ExecuteScalar(string sql, IEnumerable<DbParameter> parameters)
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
            {
                // Add params
                foreach (DbParameter Param in parameters)
                    Command.Parameters.Add(Param);

                // Execute command, and dispose of the command
                return Command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        /// <param name="items"></param>
        /// <returns></returns>
        public object ExecuteScalar(string sql, params object[] items)
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
            {
                // Add params
                for (int i = 0; i < items.Length; i++)
                {
                    DbParameter Param = this.CreateParameter();
                    Param.ParameterName = "@P" + i;
                    Param.Value = items[i];
                    Command.Parameters.Add(Param);
                }

                // Execute command, and dispose of the command
                return Command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        public T ExecuteScalar<T>(string sql, params object[] items) where T : IConvertible
        {
            // Create the SQL Command
            using (DbCommand Command = this.CreateCommand(sql))
            {
                // Add params
                for (int i = 0; i < items.Length; i++)
                {
                    DbParameter Param = this.CreateParameter();
                    Param.ParameterName = "@P" + i;
                    Param.Value = items[i];
                    Command.Parameters.Add(Param);
                }

                // Execute command, and dispose of the command
                var value = Command.ExecuteScalar();
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="command">The SQL Command to run on this database</param>
        public T ExecuteScalar<T>(DbCommand command) where T : IConvertible
        {
            // Create the SQL Command
            using (command)
            {
                // Execute command, and dispose of the command
                var value = command.ExecuteScalar();
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
        }

        #endregion Execute Methods

        #region Query Methods

        /// <summary>
        /// Queries the database, and returns a result set
        /// </summary>
        /// <param name="sql">The SQL Statement to run on the database</param>
        /// <param name="parameters">Additional parameters are parameter values for the query.
        /// The first parameter replaces @P0, second @P1 etc etc.
        /// </param>
        /// <returns></returns>
        public IEnumerable<Dictionary<string, object>> Query(string sql, params object[] parameters)
        {
            var paramItems = new List<SqliteParameter>(parameters.Length);
            for (int i = 0; i < parameters.Length; i++)
            {
                SqliteParameter Param = this.CreateParameter();
                Param.ParameterName = "@P" + i;
                Param.Value = parameters[i];
                paramItems.Add(Param);
            }

            return this.Query(sql, paramItems);
        }

        /// <summary>
        /// Queries the database, and returns a result set
        /// </summary>
        /// <param name="sql">The SQL Statement to run on the database</param>
        /// <param name="parameters">A list of sql params to add to the command</param>
        /// <returns></returns>
        public IEnumerable<Dictionary<string, object>> Query(string sql, IEnumerable<SqliteParameter> parameters)
        {
            // Create the SQL Command
            using (SqliteCommand command = this.CreateCommand(sql))
            {
                // Add params
                foreach (SqliteParameter Param in parameters)
                    command.Parameters.Add(Param);

                // Execute the query
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    // If we have rows, add them to the list
                    if (reader.HasRows)
                    {
                        // Add each row to the rows list
                        while (reader.Read())
                        {
                            Dictionary<string, object> row = new Dictionary<string, object>(reader.FieldCount);
                            for (int i = 0; i < reader.FieldCount; ++i)
                                row.Add(reader.GetName(i), reader.GetValue(i));

                            yield return row;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Queries the database, and returns a result set
        /// </summary>
        /// <param name="sql">The SQL Statement to run on the database</param>
        /// <param name="parameters">Additional parameters are parameter values for the query.
        /// The first parameter replaces @P0, second @P1 etc etc.
        /// </param>
        /// <returns></returns>
        public IEnumerable<T> Query<T>(string sql, params object[] parameters) where T : EntityBase, new()
        {
            var paramItems = new List<SqliteParameter>(parameters.Length);
            for (int i = 0; i < parameters.Length; i++)
            {
                SqliteParameter Param = this.CreateParameter();
                Param.ParameterName = "@P" + i;
                Param.Value = parameters[i];
                paramItems.Add(Param);
            }

            // Get our Table Mapping
            Type objType = typeof(T);
            TableMapping table = TableCache.GetTableMap(objType);

            // Create the SQL Command
            using (SqliteCommand command = this.CreateCommand(sql))
            {
                // Add params
                foreach (SqliteParameter param in paramItems)
                    command.Parameters.Add(param);

                // Execute the query
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    // If we have rows, add them to the list
                    if (reader.HasRows)
                    {
                        var (pkOrdinals, columnMap) = BuildReaderMaps<T>(table, reader);

                        // Add each row to the rows list
                        while (reader.Read())
                        {
                            // Add object
                            yield return ConvertToEntity<T>(table, reader, pkOrdinals, columnMap);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Queries the database, and returns a result set
        /// </summary>
        /// <param name="sql">The SQL Statement to run on the database</param>
        /// <param name="parameters">A list of sql params to add to the command</param>
        /// <returns></returns>
        public IEnumerable<T> Query<T>(string sql, IEnumerable<SqliteParameter> parameters) where T : EntityBase, new()
        {
            // Get our Table Mapping
            Type objType = typeof(T);
            TableMapping table = TableCache.GetTableMap(objType);

            // Create the SQL Command
            using (SqliteCommand command = this.CreateCommand(sql))
            {
                // Add params
                foreach (SqliteParameter param in parameters)
                    command.Parameters.Add(param);

                // Execute the query
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    // If we have rows, add them to the list
                    if (reader.HasRows)
                    {
                        var (pkOrdinals, columnMap) = BuildReaderMaps<T>(table, reader);

                        // Add each row to the rows list
                        while (reader.Read())
                        {
                            // Add object
                            yield return ConvertToEntity<T>(table, reader, pkOrdinals, columnMap);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Executes the given Sql command and returns the result rows as entities
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<T> ExecuteReader<T>(SqliteCommand command) where T : EntityBase, new()
        {
            // Get our Table Mapping
            Type objType = typeof(T);
            TableMapping table = TableCache.GetTableMap(objType);
            command.Connection = this.Connection;

            // Create the SQL Command
            using (command)
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // If we have rows, add them to the list
                if (reader.HasRows)
                {
                    var (pkOrdinals, columnMap) = BuildReaderMaps<T>(table, reader);

                    // Add each row to the rows list
                    while (reader.Read())
                    {
                        // Add object
                        yield return ConvertToEntity<T>(table, reader, pkOrdinals, columnMap);
                    }
                }
            }
        }

        /// <summary>
        /// Executes the given Sql command and returns the result rows
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Dictionary<string, object>> ExecuteReader(SqliteCommand command)
        {
            // Create the SQL Command
            using (command)
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // If we have rows, add them to the list
                if (reader.HasRows)
                {
                    // Add each row to the rows list
                    while (reader.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>(reader.FieldCount);
                        for (int i = 0; i < reader.FieldCount; ++i)
                            row.Add(reader.GetName(i), reader.GetValue(i));

                        yield return row;
                    }
                }
            }
        }
        
        /// <summary>
        /// Peforms a SELECT query on the Entity Type, and returns the Enumerator
        /// for the Result set.
        /// </summary>
        /// <typeparam name="TEntity">The Entity Type</typeparam>
        /// <returns></returns>
        public IEnumerable<TEntity> Select<TEntity>() where TEntity : EntityBase, new()
        {
            // Get our Table Mapping
            Type objType = typeof(TEntity);
            TableMapping table = TableCache.GetTableMap(objType);
            string sql = $"SELECT * FROM {QuoteIdentifier(table.TableName)};";

            // Create the SQL Command
            using (SqliteCommand command = this.CreateCommand(sql))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // If we have rows, add them to the list
                if (reader.HasRows)
                {
                    var (pkOrdinals, columnMap) = BuildReaderMaps<TEntity>(table, reader);

                    // Return each row
                    while (reader.Read())
                        yield return ConvertToEntity<TEntity>(table, reader, pkOrdinals, columnMap);
                }
            }
        }

        /// <summary>
        /// Peforms a SELECT query on the Entity Type, and returns the Enumerator
        /// for the Result set.
        /// </summary>
        /// <typeparam name="TEntity">The Entity Type</typeparam>
        /// <returns></returns>
        public IEnumerable<TEntity> Select<TEntity>(string where) where TEntity : EntityBase, new()
        {
            // Get our Table Mapping
            Type objType = typeof(TEntity);
            TableMapping table = TableCache.GetTableMap(objType);
            string sql = $"SELECT * FROM {QuoteIdentifier(table.TableName)} WHERE " + where;

            // Create the SQL Command
            using (SqliteCommand command = this.CreateCommand(sql))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // If we have rows, add them to the list
                if (reader.HasRows)
                {
                    var (pkOrdinals, columnMap) = BuildReaderMaps<TEntity>(table, reader);

                    // Return each row
                    while (reader.Read())
                        yield return ConvertToEntity<TEntity>(table, reader, pkOrdinals, columnMap);
                }
            }
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the result 
        /// set returned by the query. Additional columns or rows are ignored.
        /// </summary>
        /// <param name="sql">The SQL statement to be executed</param>
        public SelectQueryBuilder From<T>() where T : IConvertible
        {
            SelectQueryBuilder builder = new SelectQueryBuilder(this);
            builder.Table = TableCache.GetTableMap(typeof(T)).TableName;
            return builder;
        }

        #endregion Query Methods

        #region Indexing Methods

        /// <summary>
        /// Creates an index with the specified name on the specified table
        /// </summary>
        /// <param name="name"></param>
        /// <param name="table"></param>
        /// <param name="cols"></param>
        /// <param name="options"></param>
        /// <param name="where"></param>
        public void CreateIndex(string name, string table, IndexedColumn[] cols, IndexCreationOptions options, WhereStatement where = null)
        {
            // -----------------------------------------
            // Begin the SQL generation
            // -----------------------------------------
            StringBuilder sql = new StringBuilder("CREATE ", 256);
            sql.AppendIf(options.HasFlag(IndexCreationOptions.Unique), "UNIQUE ");
            sql.Append("INDEX ");
            sql.AppendIf(options.HasFlag(IndexCreationOptions.IfNotExists), "IF NOT EXISTS ");

            // Append index name
            sql.Append($"{name} ON ");
            sql.Append(QuoteIdentifier(table, this.IdentifierQuoteMode, this.IdentifierQuoteKind));
            sql.Append("(");

            // Append columns
            int i = cols.Length;
            foreach (var col in cols)
            {
                --i;
                sql.Append(QuoteIdentifier(col.Name, this.IdentifierQuoteMode, this.IdentifierQuoteKind));
                sql.AppendIf(col.Collate != Collation.Default, $" COLLATE {col.Collate.ToString().ToUpperInvariant()}");
                sql.AppendIf(col.SortOrder == Sorting.Descending, " DESC");
                sql.AppendIf(i > 0, ", ");
            }

            // Close
            sql.Append(")");

            // Add where if we have one
            if (where != null)
            {
                sql.Append(" WHERE ");
                sql.Append(where.BuildStatement());
            }

            // -----------------------------------------
            // Execute the command on the database
            // -----------------------------------------
            using (SqliteCommand command = CreateCommand(sql.ToString()))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Drops an index with the specified name from the database. 
        /// </summary>
        /// <param name="name"></param>
        public void DropIndex(string name)
        {
            string sql = $"DROP INDEX IF EXISTS {this.QuoteIdentifier(name)}";

            // -----------------------------------------
            // Execute the command on the database
            // -----------------------------------------
            using (SqliteCommand command = CreateCommand(sql.ToString()))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Creates and returns a new <see cref="SqliteCommand"/> configured with the specified query string and
        /// optional parameters.
        /// </summary>
        /// <remarks>The caller is responsible for ensuring that the query string is properly
        /// parameterized to avoid SQL injection vulnerabilities. If a transaction is active on the connection, it will
        /// automatically be associated with the created command.</remarks>
        /// <param name="queryString">The SQL query string to be executed. This string should not include unvalidated user input to prevent SQL
        /// injection.</param>
        /// <param name="parameters">An optional collection of <see cref="DbParameter"/> objects to be added to the command. If null, no
        /// parameters are added.</param>
        /// <returns>A <see cref="SqliteCommand"/> instance configured with the specified query string and parameters. If a
        /// transaction is active, it is associated with the command.</returns>
        public SqliteCommand CreateCommand(string queryString, IEnumerable<DbParameter> parameters = null)
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = queryString;

            if (Transaction != null)
                cmd.Transaction = Transaction;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }
            }

            return cmd;
        }

        /// <summary>
        /// Creates a DbParameter using the current Database engine's Parameter object
        /// </summary>
        /// <returns></returns>
        public SqliteParameter CreateParameter() => new SqliteParameter();

        /// <summary>
        /// Begins a new database transaction
        /// </summary>
        /// <returns></returns>
        public TransactionScope BeginTransaction()
        {
            if (Transaction != null)
                throw new InvalidOperationException("A transaction is already in progress on this connection.");

            Transaction = Connection.BeginTransaction();
            return new TransactionScope(this);
        }

        /// <summary>
        /// Commits the current transaction and releases the associated resources.
        /// </summary>
        /// <remarks>This method finalizes the current transaction by committing any pending changes. 
        /// After the transaction is committed, the transaction object is set to <see langword="null" />. Ensure that a
        /// transaction is active before calling this method to avoid unexpected behavior.</remarks>
        public void CommitTransaction()
        {
            Transaction?.Commit();
            Transaction?.Dispose();
            Transaction = null;
        }

        /// <summary>
        /// Rolls back the current transaction, if one is active.
        /// </summary>
        /// <remarks>This method reverts any changes made during the transaction and resets the
        /// transaction state.  If no transaction is active, the method has no effect.</remarks>
        public void RollbackTransaction()
        {
            Transaction?.Rollback();
            Transaction?.Dispose();
            Transaction = null;
        }

        /// <summary>
        /// Converts attributes from an <see cref="SQLiteDataReader"/> to an Entity
        /// </summary>
        /// <param name="table">The <see cref="TableMapping"/> for this Entity</param>
        /// <param name="reader">The current, open DataReader object</param>
        /// <returns></returns>
        internal TEntity ConvertToEntity<TEntity>(
            TableMapping table, 
            SqliteDataReader reader, 
            int[] pkOrdinals = null,
            AttributeInfo[] columnMap = null) where TEntity : EntityBase, new()
        {
            EntityKey pkValue = default;
            Type typeKey = typeof(TEntity);

            // 1. Identity Mapping Check
            if (UseIdentityMapping && table.PrimaryKeys.Count > 0)
            {
                // Use the pre-cached ordinals if provided; otherwise, look them up once
                if (pkOrdinals == null)
                {
                    pkOrdinals = new int[table.PrimaryKeys.Count];
                    int idx = 0;
                    foreach (var pk in table.PrimaryKeys)
                        pkOrdinals[idx++] = reader.GetOrdinal(pk.ColumnName);
                }

                // Pack the identity key (Handles simple and composite keys)
                pkValue = GetIdentityKey(table, reader, pkOrdinals);

                // Check the map for an existing instance
                if (!EntityIdentityMap.TryGetValue(typeKey, out var typeMap))
                {
                    typeMap = new Dictionary<EntityKey, EntityBase>();
                    EntityIdentityMap[typeKey] = typeMap;
                }

                if (typeMap.TryGetValue(pkValue, out var existing))
                {
                    return (TEntity)existing; // Return the exact same instance!
                }
            }

            // Use reflection to map the column name to the object Property
            TEntity entity = new() { State = EntityState.Loading };
            if (table.HasVirtuals)
            {
                // Create the generator and your interceptor
                var interceptor = new EntityInterceptor(this, table);
                entity = Generator.CreateClassProxyWithTarget(entity, interceptor);
            }

            // Map each column to the property — use pre-built columnMap if available
            for (int i = 0; i < reader.FieldCount; ++i)
            {
                var attribute = columnMap?[i] ?? table.GetAttributeByColumnName(reader.GetName(i));

                if (attribute.IsEnum)
                {
                    // Enum.ToObject is O(1) 
                    var value = Enum.ToObject(attribute.UnderlyingType, reader.GetValue(i));
                    attribute.SetValue(entity, value);
                }
                else if (attribute.IsNullable && reader.IsDBNull(i))
                {
                    attribute.SetValue(entity, null);
                }
                else
                {
                    switch (attribute.UnderlyingTypeCode)
                    {
                        case TypeCode.Byte:
                            if (attribute.SetByte != null)
                                attribute.SetByte(entity, reader.GetByte(i));
                            else
                                attribute.SetValue(entity, reader.GetByte(i));
                            break;
                        case TypeCode.Int16:
                            if (attribute.SetInt16 != null)
                                attribute.SetInt16(entity, reader.GetInt16(i));
                            else
                                attribute.SetValue(entity, reader.GetInt16(i));
                            break;
                        case TypeCode.Int32:
                            if (attribute.SetInt32 != null)
                                attribute.SetInt32(entity, reader.GetInt32(i));
                            else
                                attribute.SetValue(entity, reader.GetInt32(i));
                            break;
                        case TypeCode.Int64:
                            if (attribute.SetInt64 != null)
                                attribute.SetInt64(entity, reader.GetInt64(i));
                            else
                                attribute.SetValue(entity, reader.GetInt64(i));
                            break;
                        case TypeCode.Boolean:
                            if (attribute.SetBool != null)
                                attribute.SetBool(entity, reader.GetBoolean(i));
                            else
                                attribute.SetValue(entity, reader.GetBoolean(i));
                            break;
                        case TypeCode.Decimal:
                            if (attribute.SetDecimal != null)
                                attribute.SetDecimal(entity, reader.GetDecimal(i));
                            else
                                attribute.SetValue(entity, reader.GetDecimal(i));
                            break;
                        case TypeCode.Double:
                            if (attribute.SetDouble != null)
                                attribute.SetDouble(entity, reader.GetDouble(i));
                            else
                                attribute.SetValue(entity, reader.GetDouble(i));
                            break;
                        case TypeCode.Char:
                            if (attribute.SetChar != null)
                                attribute.SetChar(entity, reader.GetChar(i));
                            else
                                attribute.SetValue(entity, reader.GetChar(i));
                            break;
                        case TypeCode.DateTime:
                            if (!reader.IsDBNull(i))
                            {
                                if (attribute.SetDateTime != null)
                                    attribute.SetDateTime(entity, reader.GetDateTime(i));
                                else
                                    attribute.SetValue(entity, reader.GetDateTime(i));
                            }
                            break;
                        default:
                            object val = reader.GetValue(i);
                            if (val is DBNull)
                                continue;
                            attribute.SetValue(entity, val);
                            break;
                    }
                }
            }

            // Store entity in identity table
            if (UseIdentityMapping && table.PrimaryKeys.Count > 0)
            {
                EntityIdentityMap[typeKey][pkValue] = entity;
            }

            // Update entity state
            entity.State = EntityState.Fresh;

            // Add object
            return entity;
        }

        /// <summary>
        /// Creates a new instance of the specified entity type.
        /// </summary>
        /// <remarks>The entity type must be mapped in the <see cref="TableCache"/>. If the mapping is
        /// not found, an exception may be thrown.</remarks>
        /// <typeparam name="TEntity">The type of the entity to create. Must derive from <see cref="EntityBase"/> and have a parameterless
        /// constructor.</typeparam>
        /// <returns>A new instance of the specified entity type.</returns>
        public TEntity CreateEntity<TEntity>() where TEntity : EntityBase, new()
        {
            var table = TableCache.GetTableMap(typeof(TEntity));
            return CreateEntity<TEntity>(table);
        }

        /// <summary>
        /// Creates a new instance of the specified entity type, optionally applying a proxy with an interceptor if the
        /// table mapping includes virtual properties.
        /// </summary>
        /// <remarks>The returned entity may include a proxy if the table mapping specifies virtual
        /// properties. This proxy enables additional behaviors, such as interception of property access or method
        /// calls.</remarks>
        /// <typeparam name="TEntity">The type of the entity to create. Must inherit from <see cref="EntityBase"/> and have a parameterless
        /// constructor.</typeparam>
        /// <param name="table">The table mapping that defines the entity's structure and metadata.</param>
        /// <returns>A new instance of <typeparamref name="TEntity"/>. If the table mapping includes virtual properties, the
        /// instance will be a proxied object with an interceptor applied.</returns>
        internal TEntity CreateEntity<TEntity>(TableMapping table) where TEntity : EntityBase, new()
        {
            // Use reflection to map the column name to the object Property
            TEntity entity = new();
            if (table.HasVirtuals)
            {
                // Create the generator and your interceptor
                var interceptor = new EntityInterceptor(this, table);
                entity = Generator.CreateClassProxyWithTarget(entity, interceptor);
            }

            // Return the proxy.
            return entity;
        }

        /// <summary>
        /// Converts a C# data type to a textual SQLite data type
        /// </summary>
        /// <param name="propertyType">The C# property type that we are converting to</param>
        /// <returns></returns>
        internal static SQLiteDataType GetSQLiteType(Type propertyType)
        {
            // Store enums as their underlying type
            if (propertyType.IsEnum)
                propertyType = Enum.GetUnderlyingType(propertyType);

            switch (Type.GetTypeCode(propertyType))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Char:
                    return SQLiteDataType.INTEGER;
                case TypeCode.String:
                case TypeCode.DateTime:
                    return SQLiteDataType.TEXT;
                case TypeCode.Object:
                    return SQLiteDataType.BLOB;
                case TypeCode.Decimal:
                    return SQLiteDataType.NUMERIC;
                case TypeCode.Double:
                    return SQLiteDataType.REAL;
                default:
                    throw new NotSupportedException($"Invalid object type conversion to \"{propertyType.Name}\".");
            }
        }

        /// <summary>
        /// Converts a <see cref="ReferentialAction"/> item to its SQLite
        /// string equivelant
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        internal static string ToSQLite(ReferentialAction action)
        {
            switch (action)
            {
                case ReferentialAction.Cascade: return "CASCADE";
                case ReferentialAction.Restrict: return "RESTRICT";
                case ReferentialAction.SetDefault: return "SET DEFAULT";
                case ReferentialAction.SetNull: return "SET NULL";
                default: return "NO ACTION";
            }
        }

        /// <summary>
        /// Takes an identifier and qoutes it if the name is a reserved keyword. Passing
        /// a prefixed identifier (ex: "table.column") is valid. The <see cref="IdentifierQuoteKind"/> 
        /// and <see cref="IdentifierQuoteMode"/> options are used when determining if the identifier
        /// needs to be quoted or not.
        /// </summary>
        /// <param name="value">The attribute name</param>
        /// <returns></returns>
        public string QuoteIdentifier(string value) => QuoteIdentifier(value, IdentifierQuoteMode, IdentifierQuoteKind);

        /// <summary>
        /// Takes an identifier and qoutes it if the name is a reserved keyword. Passing
        /// a prefixed identifier (ex: "table.column") is valid.
        /// </summary>
        /// <param name="value">The attribute name</param>
        /// <returns></returns>
        public static string QuoteIdentifier(string value, IdentifierQuoteMode mode, IdentifierQuoteKind kind)
        {
            // Lets make this simple and fast!
            if (mode == IdentifierQuoteMode.None) return value;

            // Split the value by the period seperator, and determine if any identifiers are a keyword
            var parts = value.Split('.');
            var hasKeyword = mode == IdentifierQuoteMode.All;
            if (mode == IdentifierQuoteMode.KeywordsOnly)
                hasKeyword = (parts.Length > 1) ? ContainsKeyword(parts) : IsKeyword(value);

            // Appy the quoting where needed..
            if (parts.Length > 1)
            {
                switch (mode)
                {
                    case IdentifierQuoteMode.All: return ApplyQuotes(parts, mode, kind);
                    case IdentifierQuoteMode.KeywordsOnly: return (hasKeyword) ? ApplyQuotes(parts, mode, kind) : value;
                    default: return value;
                }
            }
            else // Non-array
            {
                switch (mode)
                {
                    case IdentifierQuoteMode.All: return ApplyQuotes(value, kind);
                    case IdentifierQuoteMode.KeywordsOnly: return (hasKeyword) ? ApplyQuotes(value, kind) : value;
                    default: return value;
                }
            }
        }

        /// <summary>
        /// Performs the actual quoting of the indentifier. Passing a prefixed indentifier
        /// (ex: "table.column") is NOT valid, and should be passed to the 
        /// <see cref="ApplyQuotes(string[], IdentifierQuoteMode, IdentifierQuoteKind))"/> 
        /// method instead.
        /// </summary>
        private static string ApplyQuotes(string value, IdentifierQuoteKind kind)
        {
            if (value == "*") return value;
            var chars = EscapeChars[kind];
            return string.Create(value.Length + 2, (value, chars), (span, state) =>
            {
                span[0] = state.chars[0];
                state.value.AsSpan().CopyTo(span[1..]);
                span[^1] = state.chars[1];
            });
        }

        /// <summary>
        /// Applies quoting to each identifier parameter that needs it, based on the IdentifierQuoteMode,
        /// and chains the result back into a string.
        /// </summary>
        private static string ApplyQuotes(string[] values, IdentifierQuoteMode mode, IdentifierQuoteKind kind)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                // Do we need to apply quoting to this string?
                if (mode == IdentifierQuoteMode.All || (mode == IdentifierQuoteMode.KeywordsOnly && IsKeyword(values[i])))
                    builder.Append(ApplyQuotes(values[i], kind));
                else
                    builder.Append(values[i]);

                builder.AppendIf(i + 1 < values.Length, ".");
            }
            return builder.ToString();
        }

        /// <summary>
        /// Returns whether the specified value is an SQLite reserved keyword.
        /// Passing a prefixed attribute (ex: "table.attribute") is NOT valid, and should
        /// be passed to the <see cref="ContainsKeyword(string[])"/> method instead.
        /// </summary>
        /// <param name="value">The attribute name</param>
        /// <returns></returns>
        public static bool IsKeyword(string value)
        {
            return Keywords.Contains(value);
        }

        /// <summary>
        /// Returns whether any of the specified values is an SQLite reserved keyword.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private static bool ContainsKeyword(string[] values)
        {
            foreach (var key in values)
                if (Keywords.Contains(key))
                    return true;

            return false;
        }

        /// <summary>
        /// Gets an indentity key for a given EntityType
        /// </summary>
        /// <param name="table"></param>
        /// <param name="reader"></param>
        /// <param name="pkOrdinals"></param>
        /// <returns></returns>
        private static EntityKey GetIdentityKey(TableMapping table, SqliteDataReader reader, int[] pkOrdinals)
        {
            return pkOrdinals.Length switch
            {
                1 => new EntityKey(reader.GetValue(pkOrdinals[0])),
                2 => new EntityKey(reader.GetValue(pkOrdinals[0]), reader.GetValue(pkOrdinals[1])),
                3 => new EntityKey(reader.GetValue(pkOrdinals[0]), reader.GetValue(pkOrdinals[1]), reader.GetValue(pkOrdinals[2])),
                4 => new EntityKey(reader.GetValue(pkOrdinals[0]), reader.GetValue(pkOrdinals[1]), reader.GetValue(pkOrdinals[2]), reader.GetValue(pkOrdinals[3])),
                5 => new EntityKey(reader.GetValue(pkOrdinals[0]), reader.GetValue(pkOrdinals[1]), reader.GetValue(pkOrdinals[2]), reader.GetValue(pkOrdinals[3]), reader.GetValue(pkOrdinals[4])),
                _ => throw new NotSupportedException($"Composite keys with {pkOrdinals.Length} columns are not supported.")
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static EntityKey PackIdentityKey(TableMapping table, object[] values)
        {
            var pks = table.PrimaryKeys;
            if (values == null || values.Length != pks.Count)
                throw new ArgumentException($"Entity '{table.EntityType.Name}' requires exactly {pks.Count} primary key values.");

            return values.Length switch
            {
                1 => new EntityKey(values[0]),
                2 => new EntityKey(values[0], values[1]),
                3 => new EntityKey(values[0], values[1], values[2]),
                4 => new EntityKey(values[0], values[1], values[2], values[3]),
                5 => new EntityKey(values[0], values[1], values[2], values[3], values[4]),
                _ => throw new NotSupportedException($"Composite keys with {values.Length} columns are not supported.")
            };
        }
        
        /// <summary>
        /// Builds the per-query column ordinal map and PK ordinals from the current reader.
        /// Call once after ExecuteReader(), before the row loop.
        /// </summary>
        private (int[] pkOrdinals, AttributeInfo[] columnMap) BuildReaderMaps<T>(
            TableMapping table, SqliteDataReader reader) where T : EntityBase, new()
        {
            // Cache column name -> AttributeInfo mapping once per query
            var columnMap = new AttributeInfo[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                columnMap[i] = table.GetAttributeByColumnName(reader.GetName(i));

            // Pre-compute PK ordinals once per query
            int[] pkOrdinals = null;
            if (UseIdentityMapping && table.PrimaryKeys.Count > 0)
            {
                pkOrdinals = new int[table.PrimaryKeys.Count];
                int idx = 0;
                foreach (var pk in table.PrimaryKeys)
                    pkOrdinals[idx++] = reader.GetOrdinal(pk.ColumnName);
            }

            return (pkOrdinals, columnMap);
        }

        #endregion Helper Methods

        #region Static Properties

        internal static readonly FrozenDictionary<IdentifierQuoteKind, char[]> EscapeChars = 
            new Dictionary<IdentifierQuoteKind, char[]>
            {
                { IdentifierQuoteKind.Default, new char[2] { '"', '"' } },
                { IdentifierQuoteKind.SingleQuotes, new char[2] { '\'', '\'' } },
                { IdentifierQuoteKind.SquareBrackets, new char[2] { '[', ']' } },
                { IdentifierQuoteKind.Accents, new char[2] { '`', '`' } },
            }.ToFrozenDictionary();

        /// <summary>
        /// Gets or sets the list of SQLite reserved keywords
        /// </summary>
        public static FrozenSet<string> Keywords = new HashSet<string>(new string[]
            {
                "ABORT",
                "ACTION",
                "ADD",
                "AFTER",
                "ALL",
                "ALTER",
                "ANALYZE",
                "AND",
                "AS",
                "ASC",
                "ATTACH",
                "AUTOINCREMENT",
                "BEFORE",
                "BEGIN",
                "BETWEEN",
                "BY",
                "CASCADE",
                "CASE",
                "CAST",
                "CHECK",
                "COLLATE",
                "COLUMN",
                "COMMIT",
                "CONFLICT",
                "CONSTRAINT",
                "CREATE",
                "CROSS",
                "CURRENT_DATE",
                "CURRENT_TIME",
                "CURRENT_TIMESTAMP",
                "DATABASE",
                "DEFAULT",
                "DEFERRABLE",
                "DEFERRED",
                "DELETE",
                "DESC",
                "DETACH",
                "DISTINCT",
                "DROP",
                "EACH",
                "ELSE",
                "END",
                "ESCAPE",
                "EXCEPT",
                "EXCLUSIVE",
                "EXISTS",
                "EXPLAIN",
                "FAIL",
                "FOR",
                "FOREIGN",
                "FROM",
                "FULL",
                "GLOB",
                "GROUP",
                "HAVING",
                "IF",
                "IGNORE",
                "IMMEDIATE",
                "IN",
                "INDEX",
                "INDEXED",
                "INITIALLY",
                "INNER",
                "INSERT",
                "INSTEAD",
                "INTERSECT",
                "INTO",
                "IS",
                "ISNULL",
                "JOIN",
                "KEY",
                "LEFT",
                "LIKE",
                "LIMIT",
                "MATCH",
                "NATURAL",
                "NO",
                "NOT",
                "NOTNULL",
                "NULL",
                "OF",
                "OFFSET",
                "ON",
                "OR",
                "ORDER",
                "OUTER",
                "PLAN",
                "PRAGMA",
                "PRIMARY",
                "QUERY",
                "RAISE",
                "RECURSIVE",
                "REFERENCES",
                "REGEXP",
                "REINDEX",
                "RELEASE",
                "RENAME",
                "REPLACE",
                "RESTRICT",
                "RIGHT",
                "ROLLBACK",
                "ROW",
                "SAVEPOINT",
                "SELECT",
                "SET",
                "TABLE",
                "TEMP",
                "TEMPORARY",
                "THEN",
                "TO",
                "TRANSACTION",
                "TRIGGER",
                "UNION",
                "UNIQUE",
                "UPDATE",
                "USING",
                "VACUUM",
                "VALUES",
                "VIEW",
                "VIRTUAL",
                "WHEN",
                "WHERE",
                "WITH",
                "WITHOUT",
            },
            StringComparer.OrdinalIgnoreCase) .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        #endregion

        /// <summary>
        /// Provides a scope for managing database transactions, ensuring that transactions are either committed or
        /// rolled back to maintain data consistency.
        /// </summary>
        /// <remarks>Use this class to encapsulate a block of code that requires transactional behavior.
        /// The transaction is automatically rolled back if <see cref="Commit"/> is not explicitly called before the
        /// scope is disposed. This ensures that any uncommitted changes are reverted, maintaining the integrity of the
        /// database.</remarks>
        public class TransactionScope : IDisposable
        {
            private readonly SQLiteContext _context;

            internal TransactionScope(SQLiteContext context)
            {
                _context = context;
            }

            public void Commit()
            {
                _context.CommitTransaction();
            }

            public void Rollback()
            {
                _context.RollbackTransaction();
            }

            /// <summary>
            /// Releases all resources used by the current instance and ensures that any active transaction is rolled
            /// back if not committed.
            /// </summary>
            /// <remarks>If the transaction is still active when this method is called, it will be
            /// rolled back to maintain data consistency.  Call <see cref="Commit"/> before disposing to finalize the
            /// transaction and avoid a rollback.</remarks>
            public void Dispose()
            {
                // If the transaction is still active when this is called,
                // it means Commit() was never called, so we should roll back.
                if (_context.Transaction != null)
                {
                    _context.RollbackTransaction();
                }
            }
        }
    }
}
