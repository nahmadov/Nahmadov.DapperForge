using System.Linq.Expressions;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Computes a structural hash code for expression trees based on their structure,
/// node types, and semantic content rather than ToString() representation.
/// </summary>
/// <remarks>
/// This hasher ensures that structurally equivalent expressions produce the same hash code,
/// regardless of parameter names or .NET version differences in ToString() output.
/// </remarks>
internal sealed class ExpressionStructuralHasher : ExpressionVisitor
{
    private int _hashCode;

    /// <summary>
    /// Computes a structural hash code for the given expression.
    /// </summary>
    public static int ComputeHash(Expression expression)
    {
        var hasher = new ExpressionStructuralHasher();
        hasher.Visit(expression);
        return hasher._hashCode;
    }

    private ExpressionStructuralHasher()
    {
        _hashCode = 17; // Prime number seed
    }

    private void CombineHash(int value)
    {
        unchecked
        {
            _hashCode = (_hashCode * 31) + value;
        }
    }

    private void CombineHash(object? obj)
    {
        if (obj is null)
        {
            CombineHash(0);
        }
        else
        {
            CombineHash(obj.GetHashCode());
        }
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            CombineHash(0);
            return null;
        }

        // Combine node type into hash
        CombineHash((int)node.NodeType);
        CombineHash((int)node.Type.GetHashCode());

        return base.Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        CombineHash(nameof(BinaryExpression));
        CombineHash((int)node.NodeType);

        if (node.Method is not null)
        {
            CombineHash(node.Method.DeclaringType?.FullName);
            CombineHash(node.Method.Name);
        }

        Visit(node.Left);
        Visit(node.Right);
        Visit(node.Conversion);

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        CombineHash(nameof(ConstantExpression));

        // For constants, include the type and value
        CombineHash(node.Type.FullName);

        if (node.Value is null)
        {
            CombineHash(0);
        }
        else
        {
            // Hash the value, but handle special cases
            var value = node.Value;
            var valueType = value.GetType();

            if (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(decimal))
            {
                // Primitive types and strings: use their hash code
                CombineHash(value);
            }
            else
            {
                // For complex types (closures, etc.), use type name only
                // We don't want to hash the actual closure instance
                CombineHash(valueType.FullName);
            }
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        CombineHash(nameof(MemberExpression));
        CombineHash(node.Member.DeclaringType?.FullName);
        CombineHash(node.Member.Name);
        CombineHash((int)node.Member.MemberType);

        Visit(node.Expression);

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        CombineHash(nameof(MethodCallExpression));
        CombineHash(node.Method.DeclaringType?.FullName);
        CombineHash(node.Method.Name);
        CombineHash(node.Method.GetParameters().Length);

        Visit(node.Object);

        foreach (var arg in node.Arguments)
        {
            Visit(arg);
        }

        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        CombineHash(nameof(ParameterExpression));
        // Don't hash parameter name - structurally equivalent expressions
        // should have same hash regardless of parameter names
        CombineHash(node.Type.FullName);

        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        CombineHash(nameof(LambdaExpression));
        CombineHash(node.Parameters.Count);
        CombineHash(node.ReturnType.FullName);

        // Visit body (parameters are visited through the body)
        Visit(node.Body);

        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        CombineHash(nameof(UnaryExpression));
        CombineHash((int)node.NodeType);

        if (node.Method is not null)
        {
            CombineHash(node.Method.DeclaringType?.FullName);
            CombineHash(node.Method.Name);
        }

        Visit(node.Operand);

        return node;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        CombineHash(nameof(ConditionalExpression));
        Visit(node.Test);
        Visit(node.IfTrue);
        Visit(node.IfFalse);
        return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        CombineHash(nameof(NewExpression));
        CombineHash(node.Constructor?.DeclaringType?.FullName);
        CombineHash(node.Arguments.Count);

        foreach (var arg in node.Arguments)
        {
            Visit(arg);
        }

        return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        CombineHash(nameof(NewArrayExpression));
        CombineHash((int)node.NodeType);
        CombineHash(node.Expressions.Count);

        foreach (var expr in node.Expressions)
        {
            Visit(expr);
        }

        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        CombineHash(nameof(MemberInitExpression));
        Visit(node.NewExpression);
        CombineHash(node.Bindings.Count);

        foreach (var binding in node.Bindings)
        {
            CombineHash(binding.Member.Name);
            CombineHash((int)binding.BindingType);
        }

        return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        CombineHash(nameof(ListInitExpression));
        Visit(node.NewExpression);
        CombineHash(node.Initializers.Count);

        foreach (var initializer in node.Initializers)
        {
            CombineHash(initializer.Arguments.Count);
        }

        return node;
    }
}
