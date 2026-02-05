using System.Linq.Expressions;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Querying.Includes;

public class IncludeExpressionResult
{
    public PropertyInfo Property { get; init; } = null!;
    public LambdaExpression? Filter { get; init; }
}