# CrossLite
CrossLite is an open source, minimal library to allow .NET applications to store, fetch and translate data in SQLite 3 databases.This library is very similar to Entity Framework by Microsoft, but much more lightweight and supports SQLite CodeFirst. Unlike Entity Framework however, all references to data are not stored by this library. When you query for an entity, it comes directly from the database, regardless to whether that entity has a reference already.
__Please bear with me as I complete this Readme, it is still a work in progress__.

#### Table of Contents

- [Requirements](#requirements)
- [Basic Read Query Example](#basic-read-query-example)
- [Basic Insert Query](#basic-insert-query)
- [The DbSet: Moving away from SQL queries](#the-dbset-moving-away-from-sql-queries)
- [CodeFirst](#codefirst)
- [CodeFirst Entity](#codefirst-entity)

#### Requirements
 * Castle.Core 5.2.1
 * Microsoft.Data.Sqlite 9.0.8
 * Microsoft .NET 8

#### Basic Read Query Example
```C#
var builder = new SqliteConnectionStringBuilder() { DataSource = filePath };
using (var context = new SQLiteContext(builder))
{
    // Open the database connection
    context.Connect();
    
	// Fetch account entities in a basic query
	IEnumerable<Account> accounts = context.Query<Account>("SELECT * FROM Account WHERE Id > 0");

	// Connection is still open until we enumerate the results, or cast to an ToArray() or ToList()'
	foreach (Account account in accounts)
	{
		// .. do stuff
	}
	
	// Context is closed automatically when the context is disposed, therfore
	// there is no requirement to call context.Close()
}
```
Lets take a look at the Account Entity next:
```C#
using CrossLite;

namespace CrossLiteExample
{
    public class Account : EntityBase
    {
        [Column]
        public virtual int Id { get; set; }

        [Column]
        public virtual string Name { get; set; }
    }
}
```
The `[Column]` attribute tells the Entity Translator that the properties "Id" and "Name" are mapped to the same named columns on the table "Account". You can optionally put the table's column name in the Column attribute if it differs from that in the table (`[Column("name")]`). Lets have a look at a Non query next.
All entities must extend from the abstract EntityBase class, for dirty property tracking. All column, foreign key and childset properties must also be marked Virtual.

#### Basic Insert Query
```C#
var builder = new SQLiteConnectionStringBuilder() { DataSource = filePath };
using (var context = new SQLiteContext(builder))
{
    // Open the database connection
    context.Connect();
    
    // --- Method 1: Using an SQLiteParameter list
	List<SQLiteParameter> parameters = new List<SQLiteParameter>();
	parameters.Add(new SQLiteParameter() { ParameterName = "@P0", Value = 2 });
	parameters.Add(new SQLiteParameter() { ParameterName = "@P1", Value = "Steve" });

	// Perform insertion
	int rowsAffected = context.Execute("INSERT INTO Account(Id, Name) VALUES(@P0, @P1)", parameters);
	
	// --- Method 2: Just pass an unlimited amount of ordered parameters
	int rowsAffected = context.Execute("INSERT INTO Account(Id, Name) VALUES(@P0, @P1)", 1, "Dave");
	
	// Success handling
	if (rowsAffected > 0)
	{
		// success...
	}
}
```

### The DbSet: Moving away from SQL queries
For those more interested in managing thier data in C# type syntax, rather than writing SQL, CrossLite comes with an awesome `DbSet<TEntity>` class. The DbSet represents the collection of all Entities (rows of data) in the context that can be queried from the database. The DbSet object implements the `IEnumerable<T>` interface, which comes directly from the SQLite database, allowing you to use LINQ on the Entities. Let me show you a basic example of a DbSet in action.

```C#
using CrossLite;

var builder = new SQLiteConnectionStringBuilder() { DataSource = filePath };
using (var context = new DerivedContext(builder))
{
    // Open the database connection
    context.Connect();
    
    // Insert some dummy data
    Account entity = context.CreateEntity<Account>(); // You can also use "context.Accounts.Create();"
	entity.Name = "Steve";
    context.Accounts.Add(entity);
    
    // Update the data
    entity.Name = "Dave";
    context.Accounts.Update(entity);
    
    // Fetch data using LINQ queries
    entity = (from x in context.Accounts select x).First();
    
    // OR fetching using LINQ methods
    entity = context.Accounts.First();
    
    // Name will be Dave
    Debug.AssertEquals(entity.Name == "Dave");
}

// And our custom derivied context we used in this example

public class DerivedContext : SQLiteContext
{
	/// <summary>
	/// The "Accounts" table in our database
	/// </summary>
	public DbSet<Account> Accounts { get; set; }

	public DerivedContext(string conn) : base(conn)
	{
		// Setup our Database Sets
		this.Accounts = new DbSet<Account>(this);
	}
}
```

### CodeFirst
What is CodeFirst? CodeFirst is a set of features that allows you to design your database based off of your Entity objects, rather than designing your Entities around your database. In order to use CodeFirst features, you must add the CodeFirst namespace in addition to the CrossLite namespace. Lets have a look at the 2 methods in CodeFirst that allow you to Create and Drop tables using Entity types.

```C#
using CrossLite;
using CrossLite.CodeFirst;

var builder = new SqliteConnectionStringBuilder() { DataSource = filePath };
using (var context = new DerivedContext(builder))
{
    // Drop table
    context.DropTable<Account>();

    // Create new table
    context.CreateTable<Account>();
}
```
To be able to create and drop tables by just passing an Entity to CodeFirst, you must attach some new attributes to your Properties. In the next section, we will see an example of a CodeFirst entity.

#### CodeFirst Entity
One of the many great features of CodeFirst, is the addition to Foreign Key support and loading in Entities. With new features like that however, comes more complexity. Have a look at the Account entity now!

```C#
using CrossLite;
using CrossLite.CodeFirst;

namespace MyProject
{
    [Table]
    public class Account : EntityBase
    {
        [Column, PrimaryKey]
        public virtual int Id { get; set; }

        [Column, Required, Collation(Collation.NoCase)]
        public virtual string Name { get; set; }
        
        [Column]
        public virtual int RoleId { get; set; }

        /// <summary>
        /// A lazy loaded enumeration that fetches all Privilages
        /// that are bound by the foreign key and this Account.Id.
        /// </summary>
        public virtual EntitySet<UserPrivilege> Privilages { get; set; }
        
        /// <summary>
        /// The "UserRole" object where UserRole.Id equals this Account.RoleId.
        /// </summary>
        [ForeignKey("RoleId")]
		[References("Id", OnDelete = ReferentialIntegrity.Cascade)]  
        public virtual UserRole Role { get; set; }
    }
}
```