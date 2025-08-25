using CrossLite;

namespace CrossLiteTester
{
    public class TestContext : SQLiteContext
    {
        /// <summary>
        /// The "Users" table in our database
        /// </summary>
        public DbSet<Account> Users { get; set; }

        public DbSet<Privilege> Privs { get; set; }

        public DbSet<UserPrivilege> UserPrivileges { get; set; }

        public TestContext(string conn) : base(conn)
        {
            // Connect to the SQLite database file
            base.Connect();

            // Setup our Database Sets
            this.Users = new DbSet<Account>(this);
            this.Privs = new DbSet<Privilege>(this);
            this.UserPrivileges = new DbSet<UserPrivilege>(this);
        }
    }
}
