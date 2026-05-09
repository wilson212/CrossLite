namespace CrossLite.CodeFirst
{
    /// <summary>
    ///     <para>
    ///         Referential integrity is a database concept that ensures that relationships 
    ///         between tables remain consistent.
    ///     </para>
    ///     <para>
    ///         When one table has a foreign key to another table, the concept of referential 
    ///         integrity states that you may not add a record to the table that contains the 
    ///         foreign key unless there is a corresponding record in the linked table.
    ///     </para>
    ///     <para>
    ///         Referential integrity also includes the techniques known as cascading update and cascading 
    ///         delete, which ensure that changes made to the linked table are reflected in the 
    ///         primary table.
    ///     </para>
    /// </summary>
    public enum ReferentialAction
    {
        /// <summary>
        /// When a parent key is modified or deleted from the database, 
        /// no special action is taken.
        /// </summary>
        NoAction,

        /// <summary>
        /// The application is prohibited from deleting (for ON DELETE RESTRICT) 
        /// or modifying (for ON UPDATE RESTRICT) a parent key when there exists 
        /// one or more child keys mapped to it.
        /// </summary>
        Restrict,

        /// <summary>
        /// When a parent key is deleted (for ON DELETE SET NULL) or modified 
        /// (for ON UPDATE SET NULL), the child key columns of all rows in the 
        /// child table that mapped to the parent key are  set to contain SQL 
        /// NULL values.
        /// </summary>
        SetNull,

        /// <summary>
        /// Similar to "SET NULL", except that each of the child key columns is 
        /// set to contain the columns default value instead of NULL.
        /// </summary>
        SetDefault,

        /// <summary>
        /// Propagates the delete or update operation on the parent key to each 
        /// dependent child key.
        /// </summary>
        Cascade
    }
}
