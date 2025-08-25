using Microsoft.Data.Sqlite;
using System;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Provides an abstract interface for Non query builders (Insert, Update)
    /// </summary>
    public abstract class NonQueryBuilder : IDisposable
    {
        /// <summary>
        /// The database driver, if using the "BuildCommand" method
        /// </summary>
        protected SQLiteContext Context;

        /// <summary>
        /// The table name to query
        /// </summary>
        public string Table { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="NonQueryBuilder"/>
        /// </summary>
        /// <param name="context"></param>
        public NonQueryBuilder(SQLiteContext context)
        {
            this.Context = context;
        }

        /// <summary>
        /// Sets the table name we are working with
        /// </summary>
        /// <param name="table">The name of the table</param>
        public virtual void SetTable(string table) => this.Table = table;

        /// <summary>
        /// Builds the query string with the current SQL Statement, and returns
        /// the querystring. This method is NOT Sql Injection safe!
        /// </summary>
        /// <returns></returns>
        public abstract string BuildQuery();

        /// <summary>
        /// Builds the query string with the current SQL Statement, and
        /// returns the DbCommand to be executed. All WHERE paramenters
        /// are propery escaped, making this command SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public abstract SqliteCommand BuildCommand();

        /// <summary>
        /// Executes the built SQL statement on the Database connection that was passed
        /// in the contructor. All WHERE paramenters are propery escaped, 
        /// making this command SQL Injection safe.
        /// </summary>
        public abstract int Execute();

        public abstract void Dispose();
    }
}
