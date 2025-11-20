using System.Data;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace DapperToolkit.UnitTests.Fakes;

public class TestDapperDbContext(DapperDbContextOptions<TestDapperDbContext> options) : DapperDbContext(options)
{

    // Connection-ı test üçün accessible edək (ONLY tests)
    public IDbConnection ExposeConnection() => Connection;
}