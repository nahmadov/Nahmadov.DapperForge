using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace ConnectionSample;


public class AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options) : DapperDbContext(options)
{

    // helper metodlar:
    public Task<IEnumerable<UserDto>> GetActiveUsersAsync()
          => QueryAsync<UserDto>("SELECT Id, Name FROM Users WHERE IsActive = 1");
}

public sealed record UserDto(int Id, string Name);