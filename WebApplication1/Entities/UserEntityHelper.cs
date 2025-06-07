using pengdows.crud;

namespace WebApplication1;

public class UserEntityHelper : EntityHelper<UserEntity, int>
{
    public UserEntityHelper(IDatabaseContext ctx, 
        IAuditFieldResolver auditProvider)
        : base(ctx, auditProvider)
    {
    }

    public async Task<UserEntity?> FindByEmail(string email)
    {
        var list = new List<UserEntity>() { new() { Email = email } };
        var sc = BuildRetrieve(list, "u");
        var users = await LoadListAsync(sc);
        return users.FirstOrDefault();
    }

    public async Task CreateAsync(UserEntity user)
    {
        var sc = BuildCreate(user);
        await sc.ExecuteNonQueryAsync();
    }
}