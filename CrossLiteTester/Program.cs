using CrossLite;
using CrossLite.CodeFirst;
using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CrossLiteTester
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            // Delete old test database
            string database = Path.Combine(Application.StartupPath, "test.db");

            // Connect to the database
            var builder = new SqliteConnectionStringBuilder() { DataSource = database, ForeignKeys = true };
            using (TestContext db = new TestContext(builder.ToString()))
            {
                // Run test 1
                RunQueryBuilderTest(db);
                using (var trans = db.BeginTransaction())
                {
                    Console.WriteLine();
                    Console.Write("Droping Tables...");

                    // Drop tables
                    db.DropTable<UserPrivilege>();
                    db.DropTable<Privilege>();
                    db.DropTable<Account>();

                    Console.WriteLine("Success!");
                    Console.Write("Adding Tables to Database...");

                    // Create new tables
                    db.CreateTable<Account>();
                    db.CreateTable<Privilege>();
                    db.CreateTable<UserPrivilege>();

                    trans.Commit();
                    Console.WriteLine("Success!");
                    Console.WriteLine();
                }
                using (var trans = db.BeginTransaction())
                {
                    Console.Write("Adding dummy data...");

                    // Insert some dummy data
                    var entity = db.CreateEntity<Account>();
                    entity.Name = "Steve";
                    db.Users.Add(entity);

                    var ent = db.CreateEntity<Privilege>();
                    ent.Name = "Test" ;
                    db.Privs.Add(ent);

                    var up = db.CreateEntity<UserPrivilege>();
                    up.Privilege = ent;
                    up.Account = entity;
                    db.UserPrivileges.Add(up);

                    Console.WriteLine("Success!");
                    Console.WriteLine();
                    Console.WriteLine("Testing readers...");

                    // Test fetching for Fkeys
                    foreach (UserPrivilege priv in entity.Privilages)
                    {
                        var temp = priv.Privilege;
                        Console.WriteLine($"Fetched Privilege '{temp.Name}' for User '{entity.Name}'");
                    }

                    Console.WriteLine("Success!");
                    Console.WriteLine();
                    Console.Write("Fetching database entities...");
                    Console.WriteLine();

                    // Check if entity inserted correctly
                    if (!db.Users.Contains(entity))
                        Console.WriteLine("Failed!!! " + entity.Name + " does not exist!");

                    // More dummy data
                    try
                    {
                        // Test Row ID alias column
                        entity = db.CreateEntity<Account>();
                        entity.Name = "Sally";
                        db.Users.Add(entity);
                        var sally = db.Users.Select(x => x).Where(x => x.Id == 2).First();

                        // Query builder testing (Account entity name is "test" in the database)
                        var query = new SelectQueryBuilder(db);
                        query.From("test").SelectAll().Where("Id").Between(1, 2);
                        var first = query.ExecuteQuery<Account>().First();

                        Console.WriteLine($"Success!");
                        Console.WriteLine();
                        Console.WriteLine($"Account ID #{first.Id} is: {first.Name}");
                        Console.WriteLine($"Account ID #{sally.Id} is: {sally.Name}");
                        Console.WriteLine();
                        Console.Write("Fetching data count...");

                        query = new SelectQueryBuilder(db);
                        int num = query.From("test").SelectCount().ExecuteScalar<int>();
                        Console.WriteLine("Success!");
                        Console.WriteLine($"There are {num} Records!");
                        Console.WriteLine();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed!!!");
                        Console.WriteLine(e.Message);
                        var exp = e;
                    }

                    // Test an update
                    Console.WriteLine("Updating entity name to Joey..."); 
                    entity.Name = "Joey";
                    db.Users.Update(entity);

                    // Test read
                    entity = db.Users.Where(x => x.Name == "Joey").First();
                    if (entity == null)
                    {
                        Console.WriteLine("Update Failed!!! Name is not Joey");
                        Console.WriteLine();
                    }
                    else
                    {

                        Console.WriteLine("Update Success!");
                        Console.WriteLine();
                    }

                    // Delete test
                    db.Users.Remove(entity);
                    trans.Commit();
                    entity = (from x in db.Users select x).First();

                    // Multi query benchmark
                    Console.WriteLine();
                    RunQueryBuilderTests(db);

                    // Pause
                    Console.Read();
                }
            }
        }

        private static void RunQueryBuilderTest(SQLiteContext context)
        {
            // Quote keywords only with accents
            context.IdentifierQuoteKind = IdentifierQuoteKind.Accents;
            context.IdentifierQuoteMode = IdentifierQuoteMode.KeywordsOnly;

            // Start logging times
            Stopwatch timer = Stopwatch.StartNew();

            // Simple query test, using a reserved word for quote testing
            var query = new SelectQueryBuilder(context);
            query.From("table1")                                // Indicate table (can be done after select)
                .Select("col1", "col2", "plan")                 // Main table select
                .As("Id", "Name")                               // Alias 2 of the 3 columns (zero-based index)
                .InnerJoin("table2").As("t2").On("col22").Equals("table1", "col1")
                .Select("col21", "col22")                       // Inner join a table, selecting 2 columns
                .Alias(1, "Type")                               // Alias at zero-based index
                .CrossJoin("table3").As("t3").Using("Id")       // Cross Join another table!
                .SelectCount()                                  // I could specify the return alias here, but for testing reasons...
                .As("count")                                    // ...We are going to do this inefficiently for a testing reason!
                // Finally, a where clause
                .Where("Type").Equals(AccountType.Admin).And("Id").GreaterThan(6).Or("plan").NotEqualTo(3);
            var queryString = query.BuildQuery();

            // Log query builder time (10ms for me on an i7-14700KF)
            long time = timer.ElapsedMilliseconds;

            Console.WriteLine("Complicated QueryBuilder Test:");
            Console.WriteLine();
            Console.WriteLine(queryString);
            Console.WriteLine($"Query generated in {time}ms");
        }

        private static void RunQueryBuilderTests(SQLiteContext context)
        {
            // No quotes
            context.IdentifierQuoteMode = IdentifierQuoteMode.None;

            // Run the queries, both plain and query builder
            double timesToRun = 10000;
            StringBuilder builder = new StringBuilder();

            // Write to console
            Console.WriteLine("Testing querybuilder against plain SQL ({0:N0} queries)", timesToRun);

            // Start logging times
            Stopwatch timer = Stopwatch.StartNew();

            for (double i = timesToRun; i > 0; i--)
            {
                // Clear buffer
                builder.Clear();
                builder.Append("SELECT * FROM test WHERE Id BETWEEN 1 AND 4");
                var result = context.Query<Account>(builder.ToString());
            }

            // Log time
            long time1 = timer.ElapsedMilliseconds;
            Console.WriteLine("Plain Query:     {0}ms   ({1}ms per query)", time1, Math.Round(time1 / timesToRun, 8));
            timer.Restart();

            for (double i = timesToRun; i > 0; i--)
            {
                var query = new SelectQueryBuilder(context);
                //query.From("test").Select("Id", "Name", "AccountType", "Col1", "Col2", "Col3").Where("Id").Between(1, 4);
                query.From("test").SelectAll().Where("Id").Between(1, 4);
                var result2 = query.ExecuteQuery<Account>();
            }

            // Log Time
            long time2 = timer.ElapsedMilliseconds;
            Console.WriteLine("QueryBuilder:    {0}ms   ({1}ms per query)", time2, Math.Round(time2 / timesToRun, 8));
        }
    }
}
