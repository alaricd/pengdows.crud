using System.Data;
using pengdows.crud.attributes;

namespace WebApplication1;

public class UserEntity
{
    [Id]
    [Column("Id", DbType.Int32)]
    public int ID { get; set; }

    [PrimaryKey]
    [Column("Email", DbType.String)]
    public string Email { get; set; } = "";

    [CreatedBy]
    [Column("CreatedBy", DbType.String)]
    public int CreatedBy { get; set; }

    [CreatedOn]
    [Column("CreatedOn", DbType.DateTime)]
    public DateTime CreatedOn { get; set; }
}
