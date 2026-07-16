using CrossLite;
using CrossLite.CodeFirst;

namespace CrossLiteTester
{
    [Table]
    public class Privilege : EntityBase
    {
        [PrimaryKey]
        [Column("id")]
        public virtual int Id { get; set; }

        [Column("name"), Required, Unique]
        public virtual string Name { get; set; }   
    }
}
