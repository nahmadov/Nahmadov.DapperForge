using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Oracle.Context;

public class DapperDbContext(IDapperConnectionProvider provider) : Core.Context.DapperDbContext(provider)
{
    public IDapperDbSet<T> Set<T>() where T : class
    {
        return new DapperDbSet<T>(this);
    }
}