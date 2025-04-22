using pengdows.crud;

namespace WebApplication1;

public class UserService
{
    private readonly IEntityHelper<UserEntity, int> _helper;

    public UserService(IEntityHelper<UserEntity, int> helper)
    {
        _helper = helper;
    }

    public Task CreateAsync(UserEntity user) => _helper.CreateAsync(user);

    public Task<UserEntity?> FindByEmail(string email) =>
        (_helper as UserEntityHelper)?.FindByEmail(email) ?? Task.FromResult<UserEntity?>(null);
}