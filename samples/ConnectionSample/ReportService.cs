namespace ConnectionSample;

public class ReportService(AppDapperDbContext db)
{
    private readonly AppDapperDbContext _db = db;

    public Task<int> DeleteOldLogsAsync()
          => _db.ExecuteAsync(
              "DELETE FROM Logs WHERE CreatedDate < @cut",
              new { cut = DateTime.UtcNow.AddMonths(-1) });

    public Task<IEnumerable<UserDto>> GetUsersAsync()
        => _db.QueryAsync<UserDto>("SELECT Id, Name FROM Users");
}