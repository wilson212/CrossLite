using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Provides an object interface that can properly put together an SQL Reader Query string.
    /// </summary>
    /// <remarks>
    /// All parameters in the WHERE and HAVING statements will be escaped by the underlaying
    /// DbCommand object when using the BuildCommand() method. The Execute*() methods are 
    /// SQL injection safe and will properly escape attribute values in the query.
    /// ----
    /// This is bi-directional builder, meaning you can call the methods in this class
    /// in any order, and will still get the same output.
    /// ---
    /// </remarks>
    /// <example>
    /// 
    /// Simple (select all):
    ///     var builder = new SelectQueryBuilder(context);
    ///     builder.From(tableName).SelectAll();
    ///     SQLiteCommand command = builder.BuildCommand();
    ///     
    /// Simple (with Where statement):
    ///     var builder = new SelectQueryBuilder(context);
    ///     builder.From(tableName)
    ///         .Select("col1", "col2", "col3")
    ///         .Where("id").Between(1, 10);
    ///     SQLiteCommand command = builder.BuildCommand();
    ///     
    /// Advanced Select:
    ///     var builder = new SelectQueryBuilder(context);
    ///     builder.From(tableName)
    ///         .Select("col1", "col2", "col3")
    ///             .As("id", "name", "is_admnin")
    ///         .Where("id").GreaterThan(1)
    ///             .And("is_admin").Equals(true)
    ///         .OrderBy("id", Sorting.Descending)
    ///         .Skip(10)
    ///         .Take(5);
    ///     SQLiteCommand command = builder.BuildCommand();
    ///     
    /// Advanced Select with a Join:
    ///     var builder = new SelectQueryBuilder(context);
    ///     builder.From(tableName, "table1")
    ///         .Select("col1", "col2", "col3")
    ///         .InnerJoin("table2").As("t2").On("col22").Equals("table1", "col1")
    ///         .Select("col22", "col23") --> Call Select again to grab columns from the joined table
    ///         .Where("col1").NotEqualTo(1)
    ///             .And("col22").In(1, 3, 5, 7, 9)
    ///         .OrderBy("col1", Sorting.Descending)
    ///     string query = builder.BuildQuery();
    ///     
    /// </example>
    public class SelectQueryBuilder : IDisposable
    {
        #region Public Properties

        /// <summary>
        /// The SQLiteContext attached to this builder
        /// </summary>
        public SQLiteContext Context { get; protected set; }

        /// <summary>
        /// Gets or Sets whether this Select statement will be distinct
        /// </summary>
        public bool Distinct { get; set; } = false;

        /// <summary>
        /// Gets or Sets the selected table for this query
        /// </summary>
        public string Table
        {
            get { return (Tables.Count > 0) ? Tables[0].Name : null;  }
            set { From(value); }
        }

        /// <summary>
        /// Gets a sorted list of selected tables
        /// </summary>
        public List<TableIndentifier> Tables { get; set; }

        /// <summary>
        /// The Where statement for this query
        /// </summary>
        public SelectWhereStatement WhereStatement { get; set; }

        /// <summary>
        /// The Having statement for this query
        /// </summary>
        public SelectWhereStatement HavingStatement { get; set; }

        /// <summary>
        /// Specifies the number of rows to return, after processing the OFFSET clause.
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// Specifies the number of rows to skip, before starting to return rows from the query expression.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Gets a list of columns this query will Group By
        /// </summary>
        public List<string> GroupByColumns { get; set; } = new List<string>();

        /// <summary>
        /// Gets a list of current Order By Statements on this query
        /// </summary>
        public List<OrderByClause> OrderByStatements { get; set; } = new List<OrderByClause>();

        /// <summary>
        /// Gets a list of Table Joins on this query
        /// </summary>
        public List<JoinClause> Joins { get; set; } = new List<JoinClause>();

        /// <summary>
        /// Gets a list of columns this query will Group By
        /// </summary>
        public List<UnionStatement> Unions { get; set; } = new List<UnionStatement>(2);

        /// <summary>
        /// Gets or sets the next column index since the last As() or Select*() method call. This
        /// counter is used for the .As() method to track when to begin the selection of
        /// columns
        /// </summary>
        private int NextAliasIndex { get; set; } = 0;

        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="SelectQueryBuilder"/> with the provided SQLite connection.
        /// </summary>
        /// <param name="context">The SQLiteContext that will be used to build and query this SQL statement</param>
        public SelectQueryBuilder(SQLiteContext context)
        {
            this.Context = context;
            this.Tables = new List<TableIndentifier>();

            // Set qouting modes
            this.WhereStatement = new SelectWhereStatement(this);
            this.HavingStatement = new SelectWhereStatement(this);
        }

        #region Select Cols

        /// <summary>
        /// Selects all columns in the SQL Statement being built
        /// </summary>
        public SelectQueryBuilder SelectAll()
        {
            // No columns in the selection list equal select all
            if (Tables.Count == 0)
                Tables.Add(new TableIndentifier("", null));
            else
                Tables.Last().Columns.Clear();

            // Skip this identifier since we can't alias a multi-select
            NextAliasIndex++;
            return this;
        }

        /// <summary>
        /// Selects a specified column in the SQL Statement being built. Calling this method
        /// clears all previous selected columns
        /// </summary>
        /// <param name="column">The Column name to select</param>
        public SelectQueryBuilder SelectColumn(string column, string alias = null, bool escape = true)
        {
            // Ensure created with main table index
            if (Tables.Count == 0)
                Tables.Add(new TableIndentifier("", null));

            // Grab the current working table
            var table = Tables.Last();

            // Update next alias index
            NextAliasIndex = table.Columns.Count;

            // Add item to list
            table.Columns[column] = new ColumnIdentifier(column, alias, escape);

            // Allow chaining
            return this;
        }

        /// <summary>
        /// Adds the specified column selectors in the SQL Statement being built.
        /// </summary>
        /// <param name="columns">The column names to select</param>
        public SelectQueryBuilder Select(params string[] columns)
        {
            // Make sure the array isnt empty...
            if (columns.Count() == 0) return this;

            // Ensure created with main table index
            if (Tables.Count == 0)
                Tables.Add(new TableIndentifier("", null));

            // Grab the current working table
            var table = Tables.Last();

            // Update next alias index
            NextAliasIndex = table.Columns.Count;

            // Add columns
            foreach (string col in columns)
                table.Columns[col] = new ColumnIdentifier(col);

            // Allow chaining
            return this;
        }

        /// <summary>
        /// Adds the specified column selectors in the SQL Statement being built.
        /// </summary>
        /// <param name="columns">The column names to select</param>
        public SelectQueryBuilder Select(IEnumerable<string> columns)
        {
            // Make sure the array isnt empty...
            if (columns.Count() == 0) return this;

            // Ensure created with main table index
            if (Tables.Count == 0)
                Tables.Add(new TableIndentifier("", null));

            // Grab the current working table
            var table = Tables.Last();

            // Update next alias index
            NextAliasIndex = table.Columns.Count;

            // Add columns
            foreach (string col in columns)
                table.Columns[col] = new ColumnIdentifier(col);

            // Allow chaining
            return this;
        }

        #endregion Select Cols

        #region Aggregates

        /// <summary>
        /// Adds an aggregate selection to the current table selector
        /// </summary>
        /// <param name="column">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        /// <param name="type">The aggregate type</param>
        /// <returns></returns>
        internal SelectQueryBuilder Aggregate(string column, string alias, AggregateFunction type)
        {
            // Ensure created with main table index
            if (Tables.Count == 0)
                Tables.Add(new TableIndentifier("", null));

            // Ensure the column name is correct
            if (String.IsNullOrWhiteSpace(column))
            {
                // Only count can be null or empty
                if (type != AggregateFunction.Count)
                {
                    string name = Enum.GetName(typeof(AggregateFunction), type).ToUpperInvariant();
                    throw new ArgumentException("No column name specified for the aggregate '{name}'", "type");
                }
                else
                {
                    // Just wildcard the name
                    column = "*";
                }
            }
            else if (type != AggregateFunction.Count && column.Equals("*"))
            {
                string name = Enum.GetName(typeof(AggregateFunction), type).ToUpperInvariant();
                throw new ArgumentException($"Cannot use a wildcard in place of an column name for the aggregate '{name}'", "type");
            }

            // Grab the current working table
            var table = Tables.Last();

            // Update insertion count
            NextAliasIndex = table.Columns.Count;

            // Get column key and Add item to list
            string key = String.Format(ColumnIdentifier.GetAggregateString(type), column);
            table.Columns[key] = new ColumnIdentifier(column, alias, true)
            {
                Aggregate = type
            };
            return this;
        }

        /// <summary>
        /// The count(X) function returns a count of the number of times that X is not NULL in a group. 
        /// The count(*) function (with no arguments) returns the total number of rows in the group.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectCount(string columnName = "*", string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.Count);

        /// <summary>
        /// The count(distinct X) function returns the number of distinct values of column X instead of the 
        /// total number of non-null values in column X.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectDistinctCount(string columnName, string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.DistinctCount);

        /// <summary>
        /// The avg() function returns the average value of all non-NULL X within a group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectAverage(string columnName, string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.Average);

        /// <summary>
        /// The min() aggregate function returns the minimum non-NULL value of all values in the group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectMin(string columnName, string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.Min);

        /// <summary>
        /// The max() aggregate function returns the maximum value of all values in the group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectMax(string columnName, string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.Max);

        /// <summary>
        /// The sum() aggregate functions return sum of all non-NULL values in the group.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectSum(string columnName, string alias = null)
            => Aggregate(columnName, alias, AggregateFunction.Sum);

        #endregion

        #region From Table


        /// <summary>
        /// Sets the table name to be used in this SQL Statement
        /// </summary>
        /// <param name="table">The table name</param>
        public SelectQueryBuilder From(string table, string alias = null)
        {
            // Ensure we are not null
            if (String.IsNullOrWhiteSpace(table))
                throw new ArgumentNullException("Tablename cannot be null or empty!", "table");

            // Ensure created with main table index
            if (Tables.Count == 0)
            {
                Tables.Add(new TableIndentifier(table, alias));
            }
            else
            {
                Tables[0].Name = table;
                Tables[0].Alias = alias;
            }

            return this;
        }

        #endregion From Table

        #region Alias

        /// <summary>
        /// Assigns a column identifier an alias name for this query.
        /// DatabaseColumns will be aliased by the order they specified in the
        /// last Select*() method.
        /// </summary>
        /// <param name="names"></param>
        /// <remarks>This is an O(n) operation; where n is the count of the params</remarks>
        public SelectQueryBuilder As(params string[] names)
        {
            // Attempt to grab working table
            var table = Tables.LastOrDefault();

            // Make sure we have a table and some columns
            if (table == null || table.Columns.Count == 0)
                throw new Exception("Method call on \"AS\" not valid on a blank query! Please select a table and some columns first!");

            // Make sure the count is good
            if (names.Length > (table.Columns.Count - NextAliasIndex))
                throw new Exception($"Parameter count larger than the last selected column count on table \"{table.Name}\"");

            // Add the aliases
            foreach (string name in names)
                table.Columns[NextAliasIndex++].Alias = name;

            return this;
        }

        /// <summary>
        /// Assigns a column indentifier an alias.
        /// </summary>
        /// <param name="name">The indentifier name.</param>
        /// <param name="alias">The new alias name</param>
        /// <returns></returns>
        public SelectQueryBuilder Alias(string name, string alias)
        {
            // Ensure we are not null
            if (String.IsNullOrWhiteSpace(alias))
                throw new ArgumentNullException("alias");
            else if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            // Make sure we have an item for crying out loud!
            if (Tables.Count == 0)
                throw new Exception("Method call on \"Alias\" not valid on a blank query! Please select some columns first!");

            // Local variables
            var table = Tables.Last();

            // ensure the column name exists!
            if (!table.Columns.ContainsKey(name))
                throw new ArgumentException($"The indentifier \"{name}\" has not been selected!");

            // Set alias on item
            table.Columns[name].Alias = alias;
            return this;
        }

        /// <summary>
        /// Assigns a column indentifier an alias. Unlike the <see cref="As(string[])"/>
        /// method, the specified index must be the index at which the column name was added to this table.
        /// </summary>
        /// <param name="index">The index at which the column name was added to this table.</param>
        /// <param name="alias">The new alias name</param>
        /// <returns></returns>
        public SelectQueryBuilder Alias(int index, string alias)
        {
            // Ensure we are not null
            if (String.IsNullOrWhiteSpace(alias))
                throw new ArgumentNullException("alias");

            // Ensure we are a positive index
            if (index < 0)
                throw new ArgumentOutOfRangeException("index cannot be less than 0");

            // Make sure we have an item for crying out loud!
            if (Tables.Count == 0)
                throw new Exception("Method call on \"Alias\" not valid on a blank query! Please select some columns first!");

            // Make sure the count is good
            var table = Tables.Last();
            if (index >= table.Columns.Count)
                throw new ArgumentOutOfRangeException("Alias index is higher than the column count!", "index");

            // Set alias on item
            table.Columns[index].Alias = alias;
            return this;
        }

        /// <summary>
        /// Tells the QueryBuilder not to escape the provided column names
        /// at the specified indexes. If no arguments are suppiled, all columns
        /// in this table will not be escaped.
        /// </summary>
        /// <param name="indexes">
        /// The column indexies to perform a no escape on. If left empty, then all columns will not be escaped
        /// </param>
        /// <returns></returns>
        public SelectQueryBuilder NoEscapeOn(params int[] indexes)
        {
            // Make sure we have an item for crying out loud!
            if (Tables.Count == 0)
                throw new Exception("Method call on \"NoEscape\" not valid on a blank query! Please select some columns first!");

            // grab our table, and make sure the count is good
            var table = Tables.Last();

            // If we have specific indexes
            if (indexes.Length > 0)
            {
                // Ensure that we aren't going over the column count
                if (indexes.Max() > table.Columns.Count)
                    throw new Exception($"Max index is larger than selected column count on table \"{table.Name}\"");

                foreach (int index in indexes)
                    table.Columns[index].Escape = false;
            }
            else
            {
                // No escape on all
                foreach (ColumnIdentifier col in table.Columns.Values)
                    col.Escape = false;
            }

            return this;
        }

        /// <summary>
        /// Tells the QueryBuilder not to escape the provided column names. 
        /// If no arguments are suppiled, all columns in this table will not be escaped.
        /// </summary>
        /// <param name="columns">
        /// The column names to perform a no escape on. If left empty, then all columns will not be escaped
        /// </param>
        /// <returns></returns>
        public SelectQueryBuilder NoEscapeOn(params string[] columns)
        {
            // Make sure we have an item for crying out loud!
            if (Tables.Count == 0)
                throw new Exception("Method call on \"NoEscape\" not valid on a blank query! Please select some columns first!");

            // grab our table, and make sure the count is good
            var table = Tables.Last();

            // If we have specific indexes
            if (columns.Length > 0)
            {
                foreach (var name in columns)
                    table.Columns[name].Escape = false;
            }
            else
            {
                // No escape on all
                foreach (ColumnIdentifier col in table.Columns.Values)
                    col.Escape = false;
            }

            return this;
        }

        #endregion Alias

        #region Joins

        /// <summary>
        /// Creates a new Join clause statement fot the current query object
        /// </summary>
        protected JoinClause Join(JoinType type, string joinTable)
        {
            // Add clause to list of joins
            var table = new TableIndentifier(joinTable, null);
            var clause = new JoinClause(this, type, table);
            Joins.Add(clause);

            // Add the table to the tables list, and then reset the LastInsertIndex
            Tables.Add(table);
            NextAliasIndex = 0;
            return clause;
        }

        /// <summary>
        /// Creates a new Inner Join clause statement fot the current query object
        /// </summary>
        /// <param name="joinTable">The Joining Table name</param>
        public JoinClause InnerJoin(string joinTable) => Join(JoinType.InnerJoin, joinTable);

        /// <summary>
        /// Creates a new Cross Join clause statement fot the current query object
        /// </summary>
        /// <param name="joinTable">The Joining Table name</param>
        public JoinClause CrossJoin(string joinTable) => Join(JoinType.CrossJoin, joinTable);

        /// <summary>
        /// Creates a new Outer Join clause statement fot the current query object
        /// </summary>
        /// <param name="joinTable">The Joining Table name</param>
        public JoinClause OuterJoin(string joinTable) => Join(JoinType.OuterJoin, joinTable);

        /// <summary>
        /// Creates a new Left Join clause statement fot the current query object
        /// </summary>
        /// <param name="joinTable">The Joining Table name</param>
        public JoinClause LeftJoin(string joinTable) => Join(JoinType.LeftJoin, joinTable);

        #endregion Joins

        #region Wheres

        /// <summary>
        /// Creates a where clause to add to the query's where statement
        /// </summary>
        /// <param name="field">The column name</param>
        /// <param name="operator">The Comaparison Operator to use</param>
        /// <param name="compareValue">The value, for the column name and comparison operator</param>
        /// <returns></returns>
        public SelectWhereStatement Where(string field, Comparison @operator, object compareValue)
        {
            if (WhereStatement.InnerClauseOperator == LogicOperator.And)
                return WhereStatement.And(field, @operator, compareValue);
            else
                return WhereStatement.Or(field, @operator, compareValue);
        }

        /// <summary>
        /// Creates a where clause to add to the query's where statement
        /// </summary>
        /// <param name="field">The column name</param>
        /// <returns></returns>
        public SqlExpression<SelectWhereStatement> Where(string field)
        {
            if (WhereStatement.InnerClauseOperator == LogicOperator.And)
                return WhereStatement.And(field);
            else
                return WhereStatement.Or(field);
        }

        #endregion Wheres

        #region Orderby

        /// <summary>
        /// Adds an OrderBy clause to the current query object
        /// </summary>
        /// <param name="clause"></param>
        public SelectQueryBuilder OrderBy(OrderByClause clause)
        {
            OrderByStatements.Add(clause);
            return this;
        }

        /// <summary>
        /// Creates and adds a new Oderby clause to the current query object
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="order"></param>
        public SelectQueryBuilder OrderBy(string fieldName, Sorting order)
        {
            OrderByStatements.Add(new OrderByClause(fieldName, order));
            return this;
        }


        #endregion Orderby

        #region GroupBy

        /// <summary>
        /// Creates and adds a new Groupby clause to the current query object
        /// </summary>
        /// <param name="fieldName"></param>
        public SelectQueryBuilder GroupBy(string fieldName)
        {
            GroupByColumns.Add(fieldName);
            return this;
        }

        #endregion

        #region Having

        public SelectWhereStatement Having(string field, Comparison @operator, object compareValue)
        {
            if (HavingStatement.InnerClauseOperator == LogicOperator.And)
                return HavingStatement.And(field, @operator, compareValue);
            else
                return HavingStatement.Or(field, @operator, compareValue);
        }

        #endregion Having

        #region Union

        /// <summary>
        /// Adds a UNION clause to the query, which is used to combine the results of two or more 
        /// SELECT statements without returning any duplicate rows.
        /// </summary>
        /// <param name="table">The table to unionize</param>
        /// <returns>Returns a new instance of <see cref="SelectQueryBuilder"/> for the union query.</returns>
        public SelectQueryBuilder Union(string table)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Union,
                Query = new SelectQueryBuilder(Context).From(table)
            };
            Unions.Add(statement);
            return statement.Query;
        }

        /// <summary>
        /// Adds a UNION clause to the query, which is used to combine the results of two or more 
        /// SELECT statements without returning any duplicate rows.
        /// </summary>
        /// <param name="query">The query to compound in this query statement</param>
        /// <returns>Returns this instance of <see cref="SelectQueryBuilder"/></returns>
        public SelectQueryBuilder Union(SelectQueryBuilder query)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Union,
                Query = query
            };
            Unions.Add(statement);
            return this;
        }

        /// <summary>
        /// Adds a UNION ALL clause to the query, which is used to combine the results of two or more 
        /// SELECT statements and it does not remove duplicate rows between the various SELECT statements.
        /// </summary>
        /// <param name="table">The table to unionize</param>
        /// <returns>Returns a new instance of <see cref="SelectQueryBuilder"/> for the union query.</returns>
        public SelectQueryBuilder UnionAll(string table)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.UnionAll,
                Query = new SelectQueryBuilder(Context).From(table)
            };
            Unions.Add(statement);
            return statement.Query;
        }

        /// <summary>
        /// Adds a UNION ALL clause to the query, which is used to combine the results of two or more 
        /// SELECT statements and it does not remove duplicate rows between the various SELECT statements.
        /// </summary>
        /// <param name="query">The query to compound in this query statement</param>
        /// <returns>Returns this instance of <see cref="SelectQueryBuilder"/></returns>
        public SelectQueryBuilder UnionAll(SelectQueryBuilder query)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.UnionAll,
                Query = query
            };
            Unions.Add(statement);
            return this;
        }

        /// <summary>
        /// Adds an EXCEPT operator to the query, which is used to return all rows in this SELECT statement 
        /// that are not returned by the new SELECT statement
        /// </summary>
        /// <param name="table">The table to unionize</param>
        /// <returns>Returns a new instance of <see cref="SelectQueryBuilder"/> for the union query.</returns>
        public SelectQueryBuilder Except(string table)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Except,
                Query = new SelectQueryBuilder(Context).From(table)
            };
            Unions.Add(statement);
            return statement.Query;
        }

        /// <summary>
        /// Adds an EXCEPT operator to the query, which is used to return all rows in this SELECT statement 
        /// that are not returned by the new SELECT statement
        /// </summary>
        /// <param name="query">The query to compound in this query statement</param>
        /// <returns>Returns this instance of <see cref="SelectQueryBuilder"/></returns>
        public SelectQueryBuilder Except(SelectQueryBuilder query)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Except,
                Query = query
            };
            Unions.Add(statement);
            return this;
        }

        /// <summary>
        /// Adds an INTERSECT operator to the query, which returns the intersection of 2 or more datasets
        /// </summary>
        /// <param name="table">The table to intersect</param>
        /// <returns>Returns a new instance of <see cref="SelectQueryBuilder"/> for the union query.</returns>
        public SelectQueryBuilder Intersect(string table)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Intersect,
                Query = new SelectQueryBuilder(Context).From(table)
            };
            Unions.Add(statement);
            return statement.Query;
        }

        /// <summary>
        /// Adds an INTERSECT operator to the query, which returns the intersection of 2 or more datasets
        /// </summary>
        /// <param name="query">The query to compound in this query statement</param>
        /// <returns>Returns this instance of <see cref="SelectQueryBuilder"/></returns>
        public SelectQueryBuilder Intersect(SelectQueryBuilder query)
        {
            var statement = new UnionStatement()
            {
                Type = UnionType.Intersect,
                Query = query
            };
            Unions.Add(statement);
            return this;
        }

        #endregion

        #region Limit / Offset

        /// <summary>
        /// Bypasses the specified amount of records (offset) in the result set.
        /// </summary>
        /// <param name="">The offset in the query</param>
        /// <returns></returns>
        public SelectQueryBuilder Skip(int count)
        {
            // Set internal value
            this.Offset = count;

            // Allow chaining
            return this;
        }

        /// <summary>
        /// Specifies the maximum number of records to return in the query (limit)
        /// </summary>
        /// <param name="">The number of records to grab from the result set</param>
        /// <returns></returns>
        public SelectQueryBuilder Take(int count)
        {
            // Set internal value
            this.Limit = count;

            // Allow chaining
            return this;
        }

        #endregion

        /// <summary>
        /// Builds the query string with the current SQL Statement, and returns
        /// the querystring. This method is NOT Sql Injection safe!
        /// </summary>
        /// <returns></returns>
        public string BuildQuery() => BuildQuery(false) as String;

        /// <summary>
        /// Builds the query string with the current SQL Statement, and
        /// returns the SqliteCommand to be executed. All WHERE and HAVING paramenters
        /// are propery escaped, making this command SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public SqliteCommand BuildCommand() => BuildQuery(true) as SqliteCommand;

        /// <summary>
        /// Builds the query string or SQLiteCommand
        /// </summary>
        /// <param name="buildCommand"></param>
        /// <returns></returns>
        protected object BuildQuery(bool buildCommand)
        {
            // Define local variables
            int tableIndex = 0;
            int tableCount = Tables.Count;

            // Make sure we have a table name
            if (Tables.Count == 0 || String.IsNullOrWhiteSpace(Tables[0].Name))
                throw new Exception("No tables were specified for this query.");

            // Start Query
            StringBuilder query = new StringBuilder("SELECT ", 256);
            query.AppendIf(Distinct, "DISTINCT ");

            // Append columns from each table
            foreach (var table in Tables)
            {
                // Define local variables
                int colCount = table.Columns.Count;
                tableIndex++;
                tableCount--;

                // Create alias for this table if there is none
                if (String.IsNullOrWhiteSpace(table.Alias))
                    table.Alias = $"t{tableIndex}";

                // Check if the user wants to select all columns
                if (colCount == 0)
                {
                    query.AppendFormat("{0}.*", Context.QuoteIdentifier(table.Alias));
                    query.AppendIf(tableCount > 0, ", ");
                }
                else
                {
                    // Add each result selector to the query
                    foreach (ColumnIdentifier column in table.Columns.Values)
                    {
                        // Use the internal method to append the column string to our query
                        column.AppendToQuery(query, Context, table.Alias);

                        // If we have more results to select, append Comma
                        query.AppendIf(--colCount > 0 || tableCount > 0, ", ");
                    }
                }
            }

            // === Append main Table === //
            var fromTbl = Tables[0];
            query.Append($" FROM {Context.QuoteIdentifier(fromTbl.Name)} AS {Context.QuoteIdentifier(fromTbl.Alias)}");

            // Append Joined tables
            if (Joins.Count > 0)
            {
                foreach (JoinClause clause in Joins)
                {
                    // Convert join type to string
                    switch (clause.JoinType)
                    {
                        default:
                        case JoinType.InnerJoin:
                            query.Append(" JOIN ");
                            break;
                        case JoinType.OuterJoin:
                            query.Append(" OUTER JOIN ");
                            break;
                        case JoinType.CrossJoin:
                            query.Append(" CROSS JOIN ");
                            break;
                        case JoinType.LeftJoin:
                            query.Append(" LEFT JOIN ");
                            break;
                    }

                    // Append the join statement
                    string alias = Context.QuoteIdentifier(clause.JoiningTable.Alias);
                    query.Append($"{Context.QuoteIdentifier(clause.JoiningTable.Name)} AS {alias}");

                    // Do we have an expression?
                    if (clause.ExpressionType == JoinExpressionType.On)
                    {
                        // Try and grab the table
                        var tbl = Tables.Where(x => x.Name.Equals(clause.FromTable, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                        string fromT = tbl?.Alias ?? clause.FromTable;
                        query.Append(" ON ");
                        query.Append(
                            SqlExpression<WhereStatement>.CreateExpressionString(
                                $"{alias}.{Context.QuoteIdentifier(clause.JoiningColumn)}",
                                clause.ComparisonOperator,
                                new SqlLiteral(Context.QuoteIdentifier($"{fromT}.{clause.FromColumn}"))
                            )
                        );
                    }
                    else if (clause.ExpressionType == JoinExpressionType.Using)
                    {
                        var parts = clause.JoiningColumn.Split(',');
                        query.AppendFormat(" USING({0})", String.Join(", ", parts.Select(x => Context.QuoteIdentifier(x))));
                    }
                }
            }

            // Append Where Statement
            List<SqliteParameter> parameters = new List<SqliteParameter>();
            if (WhereStatement.HasClause)
            {
                if (buildCommand)
                    query.Append(" WHERE " + WhereStatement.BuildStatement(parameters));
                else
                    query.Append(" WHERE " + WhereStatement.BuildStatement());
            }

            // Append GroupBy
            if (GroupByColumns.Count > 0)
                query.Append(" GROUP BY " + String.Join(", ", GroupByColumns.Select(x => Context.QuoteIdentifier(x))));

            // Append Having
            if (HavingStatement.HasClause)
            {
                if (GroupByColumns.Count == 0)
                    throw new Exception("Having statement was set without Group By");

                query.Append(" HAVING " + HavingStatement.BuildStatement(parameters));
            }

            // Append OrderBy
            if (OrderByStatements.Count > 0)
            {
                int count = OrderByStatements.Count;
                query.Append(" ORDER BY");
                foreach (OrderByClause clause in OrderByStatements)
                {
                    query.Append($" {Context.QuoteIdentifier(clause.ColumnName)}");

                    // Add sorting if not default
                    query.AppendIf(clause.SortOrder == Sorting.Descending, " DESC");

                    // Append seperator if we have more orderby statements
                    query.AppendIf(--count > 0, ",");
                }
            }

            // Append Limit
            query.AppendIf(Limit > 0, " LIMIT " + Limit);
            query.AppendIf(Offset > 0, " OFFSET " + Offset);

            // Create Command
            SqliteCommand command = null;
            if (buildCommand)
            {
                command = Context.CreateCommand(query.ToString());
                command.Parameters.AddRange(parameters.ToArray());
            }

            // Return Result
            return (buildCommand) ? command as object : query.ToString();
        }

        /// <summary>
        /// Executes the built SQL statement on the SQLite database connection that was passed
        /// in the constructor. All WHERE and HAVING paramenters are escaped, making this command 
        /// SQL Injection safe.
        /// </summary>
        public T ExecuteScalar<T>() where T : IConvertible
        {
            return Context.ExecuteScalar<T>(BuildCommand());
        }

        /// <summary>
        /// Executes the built SQL statement on the SQLite database connection that was passed
        /// in the constructor. All WHERE and HAVING paramenters are escaped, making this command 
        /// SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Dictionary<string, object>> ExecuteQuery()
        {
            return Context.ExecuteReader(BuildCommand());
        }

        /// <summary>
        /// Executes the built SQL statement on the SQLite database connection that was passed
        /// in the constructor. All WHERE and HAVING paramenters are escaped, making this command 
        /// SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> ExecuteQuery<T>() where T : EntityBase, new()
        {
            return Context.ExecuteReader<T>(BuildCommand());
        }

        public void Dispose()
        {
            Tables = null;
            Joins = null;
            GroupByColumns = null;
            OrderByStatements = null;
            Unions = null;
            WhereStatement = null;

            GC.SuppressFinalize(this);
        }
    }
}
