using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Infrastructure.Utilities;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Evaluates expression trees to extract runtime values.
/// Uses LRU caching for compiled expressions.
/// </summary>
internal static class ExpressionEvaluator
{
    private static readonly LruCache<ExpressionCacheKey, Func<object?>> CompiledExpressionCache = new(maxSize: 1000);

    public static object? Evaluate(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return constant.Value;

        if (expr is MemberExpression member)
            return EvaluateMemberExpression(member);

        if (expr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            return Evaluate(unary.Operand);

        if (expr is NewArrayExpression arrayExpr)
        {
            var elementType = arrayExpr.Type.GetElementType() ?? typeof(object);
            var array = Array.CreateInstance(elementType, arrayExpr.Expressions.Count);
            for (var i = 0; i < arrayExpr.Expressions.Count; i++)
            {
                array.SetValue(Evaluate(arrayExpr.Expressions[i]), i);
            }
            return array;
        }

        if (expr.Type.IsByRefLike)
        {
            if (expr is UnaryExpression unarySpan)
                return Evaluate(unarySpan.Operand);

            if (expr is MethodCallExpression callSpan && callSpan.Arguments.Count > 0)
                return Evaluate(callSpan.Arguments[0]);

            throw new NotSupportedException("By-ref-like expressions cannot be evaluated.");
        }

        var converted = Expression.Convert(expr, typeof(object));
        var lambda = Expression.Lambda<Func<object?>>(converted);
        var cacheKey = new ExpressionCacheKey(lambda);

        var compiled = CompiledExpressionCache.GetOrAdd(cacheKey, _ => lambda.Compile(preferInterpretation: true));

        return compiled();
    }

    public static bool TryEvalToBool(Expression expr, out bool value)
    {
        var v = Evaluate(expr);
        if (v is bool b)
        {
            value = b;
            return true;
        }

        value = false;
        return false;
    }

    public static object? GetValueFromClosure(object? closureObject, MemberInfo member)
    {
        return closureObject is null
            ? null
            : member switch
            {
                FieldInfo fi => fi.GetValue(closureObject),
                PropertyInfo pi => pi.GetValue(closureObject),
                _ => throw new NotSupportedException($"Unsupported closure member type: {member.MemberType}")
            };
    }

    private static object? EvaluateMemberExpression(MemberExpression member)
    {
        object? target = null;
        if (member.Expression is not null)
        {
            if (member.Expression is ParameterExpression)
                throw new NotSupportedException("Cannot evaluate member access on a parameter expression.");

            target = Evaluate(member.Expression);
        }

        return member.Member switch
        {
            FieldInfo fi => fi.GetValue(target),
            PropertyInfo pi => pi.GetValue(target),
            _ => throw new NotSupportedException($"Unsupported member type: {member.Member.MemberType}")
        };
    }

    /// <summary>
    /// Cache key for compiled expressions based on expression tree structural hash.
    /// </summary>
    private readonly struct ExpressionCacheKey : IEquatable<ExpressionCacheKey>
    {
        private readonly int _hashCode;
        private readonly LambdaExpression _expression;

        public ExpressionCacheKey(LambdaExpression lambda)
        {
            _expression = lambda ?? throw new ArgumentNullException(nameof(lambda));
            _hashCode = ExpressionStructuralHasher.ComputeHash(lambda);
        }

        public bool Equals(ExpressionCacheKey other)
        {
            if (_hashCode != other._hashCode)
                return false;

            return ExpressionStructuralEqualityComparer.AreEqual(_expression, other._expression);
        }

        public override bool Equals(object? obj)
            => obj is ExpressionCacheKey key && Equals(key);

        public override int GetHashCode() => _hashCode;
    }
}



