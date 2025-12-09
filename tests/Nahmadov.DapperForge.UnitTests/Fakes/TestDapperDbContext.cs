using System.Data;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.UnitTests.Fakes;

public class TestDapperDbContext(DapperDbContextOptions<TestDapperDbContext> options) : DapperDbContext(options)
{

    // Connection--- test +-+ç+-n accessible edãÙk (ONLY tests)
    public IDbConnection ExposeConnection() => Connection;
}
