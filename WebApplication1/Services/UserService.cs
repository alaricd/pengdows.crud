namespace WebApplication1;

public class UserService
{
    private readonly UserEntityHelper _helper;

    public UserService(UserEntityHelper helper)
    {
        _helper = helper;
    }

    public Task CreateAsync(UserEntity user) => _helper.CreateAsync(user);

    public Task<UserEntity?> FindByEmail(string email) =>
        (_helper as UserEntityHelper)?.FindByEmail(email) ?? Task.FromResult<UserEntity?>(null);
}