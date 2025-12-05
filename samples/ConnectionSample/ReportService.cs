namespace ConnectionSample;

public class ReportService(AppDapperDbContext db)
{
    private readonly AppDapperDbContext _db = db;

    public async Task<int> DeleteOldLogsAsync()
    {
        var cutoff = DateTime.UtcNow.AddMonths(-1);
        var logs = (await _db.Logs.WhereAsync(x => x.CreatedDate < cutoff)).ToList();

        var deleted = 0;
        foreach (var log in logs)
        {
            deleted += await _db.Logs.DeleteAsync(log);
        }

        return deleted;
    }

    public Task<IEnumerable<User>> GetActiveUsersAsync(string startsWith)
    {
        // Case-insensitive LIKE support via predicate visitor
        return _db.Users.WhereAsync(u =>
             u.Name.StartsWith(startsWith) && u.IsActive,
            ignoreCase: true);
    }

    public async Task<int> AddUserAsync(string name, bool isActive)
    {
        var user = new User { Name = name, IsActive = isActive };
        return await _db.Users.InsertAsync(user);
    }
}
