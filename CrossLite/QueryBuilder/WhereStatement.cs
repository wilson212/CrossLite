namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// A standalone WHERE statement used by <see cref="UpdateQueryBuilder"/>,
    /// <see cref="DeleteQueryBuilder"/>, <see cref="DbSet{T}.Contains"/>, and any context
    /// where a query-agnostic WHERE clause is needed.
    /// 
    /// This class inherits all clause-building and SQL generation logic from
    /// <see cref="WhereStatementBase{TSelf}"/> and adds no additional members —
    /// it exists purely as the non-SELECT concrete type.
    /// </summary>
    public class WhereStatement : WhereStatementBase<WhereStatement>
    {
        /// <summary>
        /// Creates a new empty <see cref="WhereStatement"/> with default quoting settings.
        /// </summary>
        public WhereStatement() { }

        /// <summary>
        /// Creates a new <see cref="WhereStatement"/> using the identifier quoting settings
        /// from the supplied <see cref="SQLiteContext"/>.
        /// This ensures column names are quoted consistently with the context's configuration.
        /// </summary>
        /// <param name="context">The database context whose quoting settings will be applied.</param>
        public WhereStatement(SQLiteContext context) : this()
        {
            AttributeQuoteMode = context.IdentifierQuoteMode;
            AttributeQuoteKind = context.IdentifierQuoteKind;
        }
    }
}