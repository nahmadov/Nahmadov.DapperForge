namespace ConnectionSample;

public class ReportService(AppDapperDbContext db)
{
    private readonly AppDapperDbContext _db = db;

    public Task<int> DeleteOldLogsAsync()
          => _db.ExecuteAsync(
              "DELETE FROM Logs WHERE CreatedDate < @cut",
              new { cut = DateTime.UtcNow.AddMonths(-1) });

    public Task<IEnumerable<User>> GetActiveUsersAsync(string startsWith)
    {
        // Case-insensitive LIKE support via predicate visitor
        return _db.Users.WhereAsync(u =>
             u.Name.StartsWith(startsWith),
            ignoreCase: true);
    }

    public async Task<int> AddUserAsync(string name, bool isActive)
    {
        var user = new User { Name = name, IsActive = isActive };
        return await _db.Users.InsertAsync(user);
    }
}
