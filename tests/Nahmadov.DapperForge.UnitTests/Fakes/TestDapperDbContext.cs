using System.Data;

using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.UnitTests.Fakes;

public class TestDapperDbContext(DapperDbContextOptions<TestDapperDbContext> options) : DapperDbContext(options)
{
#pragma warning disable CS0618 // Type or member is obsolete - Testing legacy API
    // Connection exposed for tests (ONLY tests)
    public IDbConnection ExposeConnection() => Connection;
#pragma warning restore CS0618
}

