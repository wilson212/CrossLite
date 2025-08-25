using CrossLite;
using CrossLite.CodeFirst;

namespace CrossLiteTester
{
    [Table]
    public class UserPrivilege : EntityBase
    {
        [Column("pid"), PrimaryKey]
        public virtual int PrivilegeId { get; set; }

        [Column("uid"), PrimaryKey]
        public virtual int UserId { get; set; }

        [Column("has_privilege")]
        public virtual bool HasPrivilege { get; set; }

        /// <summary>
        /// Using "Fetch()" on this lazy loading class will retrieve
        /// the Account object that this UserPriv references
        /// </summary>
        [ForeignKey("uid")]
        [References("Id", OnDelete = ReferentialIntegrity.Cascade)]
        public virtual Account Account { get; set; }

        /// <summary>
        /// Using "Fetch()" on this lazy loading class will retrieve
        /// the Privilege object that this UserPriv references
        /// </summary>
        [ForeignKey("pid")]   
        [References("id", OnDelete = ReferentialIntegrity.Cascade)]   
        public virtual Privilege Privilege { get; set; }
    }
}
