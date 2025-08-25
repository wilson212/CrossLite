using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Represents a parameterized statement that is used to execute the same statement 
    /// repeatedly with high efficiency
    /// </summary>
    public class PreparedNonQuery : IDisposable
    {
        /// <summary>
        /// The prepared SQLite command
        /// </summary>
        private SqliteCommand Command { get; set; }

        /// <summary>
        /// A list of named parameters (ColName => Parameter) to be used in the SQLiteCommand
        /// </summary>
        private Dictionary<string, SqliteParameter> Params { get; set; }

        /// <summary>
        /// Gets the number of set parameters
        /// </summary>
        public int ParamCount => Params?.Count ?? 0;

        /// <summary>
        /// Indicates whether this object has been disposed or not.
        /// </summary>
        public bool Disposed { get; private set; } = false;

        /// <summary>
        /// Creates a new instance of <see cref="PreparedNonQuery"/>
        /// </summary>
        /// <param name="command">
        /// An SQL command shall be passed here with the <see cref="SQLiteCommand.CommandText"/> already set.
        /// All current parameters will be cleared from this command (see <see cref="SetParam(string, object)"/>).
        /// </param>
        public PreparedNonQuery(SqliteCommand command)
        {
            Command = command;
            Command.Parameters.Clear();
            Params = new Dictionary<string, SqliteParameter>();
        }

        ~PreparedNonQuery()
        {
            Dispose(false);
        }

        /// <summary>
        /// Sets the value of a parameter to be used in the next execution. If the parameter
        /// does not yet exist in the parameters list, it will be added.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public PreparedNonQuery SetParam(string name, object value)
        {
            SqliteParameter param;
            if (Params.TryGetValue(name, out param))
            {
                param.Value = value;
            }
            else
            {
                param = new SqliteParameter();
                param.ParameterName = name;
                param.Value = value;
                Params.Add(name, param);

                Command.Parameters.Add(param);
            }
            return this;
        }

        /// <summary>
        /// Sets the parameters of the <see cref="SqliteCommand"/> based on the 
        /// <see cref="ColumnAttribute"/> property values of an entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="entityTable"></param>
        public void SetParameters<T>(T obj, TableMapping entityTable) where T : EntityBase
        {
            // Generate the SQL
            foreach (var attribute in entityTable.DatabaseColumns)
            {
                // Keys go in the WHERE statement, not the SET statement
                PropertyInfo info = attribute.Value.Property;
                SetParam($"@{attribute.Key}", info.GetValue(obj));
            }
        }

        /// <summary>
        /// Executes the prepared query using the specified param values
        /// </summary>
        /// <returns></returns>
        public int Execute()
        {
            return Command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources.
                    Command.Dispose();
                    Params = null;
                }

                // Release unmanaged resources here.
            }

            Disposed = true;
        }
    }
}
