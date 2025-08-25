using CrossLite;
using CrossLite.CodeFirst;

namespace CrossLiteTester
{
    [Table("test")]
    public class Account : EntityBase
    {
        [Column, PrimaryKey]
        public virtual int Id { get; set; }

        [Column, Required, Collation(Collation.NoCase)]
        public virtual string Name { get; set; }

        [Column]
        public virtual int Col1 { get; set; }

        [Column]
        public virtual int Col2 { get; set; }

        [Column]
        public virtual int Col3 { get; set; }

        /// <summary>
        /// Test enumeration
        /// </summary>
        [Column]
        public virtual AccountType AccountType { get; set; } = AccountType.User;

        /// <summary>
        /// A lazy loaded enumeration that fetches all Privilages
        /// that are bound by the foreign key and this Account.Id
        /// </summary>
        public virtual EntitySet<UserPrivilege> Privilages { get; set; }
    }

    public enum AccountType : int
    {
        User = 3,
        Admin = 4
    }
}
