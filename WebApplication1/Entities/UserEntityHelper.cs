using System.Data;
using pengdows.crud;

namespace WebApplication1;


public class UserEntityHelper : EntityHelper<UserEntity, int>
{
    public UserEntityHelper(IDatabaseContext ctx, IAuditContextProvider<int> auditProvider)
        : base(ctx, auditProvider) { }

    public Task<UserEntity?> FindByEmail(string email)
    {
        return QuerySingleAsync(q =>
        {
            q.Query.Append("SELECT * FROM ");
            q.Query.Append(q.WrapObjectName("Users"));
            q.Query.Append(" WHERE ");
            q.AddParameterWithValue("Email", DbType.String, email);
            q.Query.Append("Email = " + q.Context.MakeParameterName("Email"));
        });
    }
}
