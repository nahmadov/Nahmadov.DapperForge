using System.Linq.Expressions;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Compares two expression trees for structural equality.
/// Two expressions are structurally equal if they have the same node types,
/// operations, and semantic structure, regardless of parameter names.
/// </summary>
internal sealed class ExpressionStructuralEqualityComparer : ExpressionVisitor
{
    private Expression? _comparand;
    private bool _areEqual = true;

    /// <summary>
    /// Determines whether two expressions are structurally equal.
    /// </summary>
    public static bool AreEqual(Expression? left, Expression? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        var comparer = new ExpressionStructuralEqualityComparer
        {
            _comparand = right
        };

        comparer.Visit(left);

        return comparer._areEqual;
    }

    private ExpressionStructuralEqualityComparer() { }

    public override Expression? Visit(Expression? node)
    {
        if (!_areEqual)
            return node; // Short-circuit if already found inequality

        if (node is null)
        {
            _areEqual = _comparand is null;
            return null;
        }

        if (_comparand is null)
        {
            _areEqual = false;
            return node;
        }

        // Check node type and expression type
        if (node.NodeType != _comparand.NodeType || node.Type != _comparand.Type)
        {
            _areEqual = false;
            return node;
        }

        // Recursively visit based on specific node type
        return base.Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (_comparand is not BinaryExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Method != other.Method)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;

        _comparand = other.Left;
        Visit(node.Left);

        if (_areEqual)
        {
            _comparand = other.Right;
            Visit(node.Right);
        }

        if (_areEqual && node.Conversion != null)
        {
            _comparand = other.Conversion;
            Visit(node.Conversion);
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_comparand is not ConstantExpression other)
        {
            _areEqual = false;
            return node;
        }

        // Compare values
        if (node.Value is null)
        {
            _areEqual = other.Value is null;
        }
        else if (other.Value is null)
        {
            _areEqual = false;
        }
        else
        {
            var nodeValue = node.Value;
            var otherValue = other.Value;
            var nodeType = nodeValue.GetType();
            var otherType = otherValue.GetType();

            if (nodeType != otherType)
            {
                _areEqual = false;
            }
            else if (nodeType.IsPrimitive || nodeType == typeof(string) || nodeType == typeof(decimal))
            {
                // For primitive types and strings, compare values
                _areEqual = Equals(nodeValue, otherValue);
            }
            else
            {
                // For complex types (closures), we consider them equal if types match
                // The actual comparison would require deep structural analysis
                _areEqual = true;
            }
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (_comparand is not MemberExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Member != other.Member)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;
        _comparand = other.Expression;
        Visit(node.Expression);
        _comparand = savedComparand;

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (_comparand is not MethodCallExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Method != other.Method)
        {
            _areEqual = false;
            return node;
        }

        if (node.Arguments.Count != other.Arguments.Count)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;

        _comparand = other.Object;
        Visit(node.Object);

        if (_areEqual)
        {
            for (int i = 0; i < node.Arguments.Count && _areEqual; i++)
            {
                _comparand = other.Arguments[i];
                Visit(node.Arguments[i]);
            }
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_comparand is not ParameterExpression other)
        {
            _areEqual = false;
            return node;
        }

        // For structural equality, we ignore parameter names
        // Only check that types match
        _areEqual = node.Type == other.Type;

        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (_comparand is not Expression<T> other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Parameters.Count != other.Parameters.Count)
        {
            _areEqual = false;
            return node;
        }

        if (node.ReturnType != other.ReturnType)
        {
            _areEqual = false;
            return node;
        }

        // Compare lambda bodies
        var savedComparand = _comparand;
        _comparand = other.Body;
        Visit(node.Body);
        _comparand = savedComparand;

        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (_comparand is not UnaryExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Method != other.Method)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;
        _comparand = other.Operand;
        Visit(node.Operand);
        _comparand = savedComparand;

        return node;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        if (_comparand is not ConditionalExpression other)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;

        _comparand = other.Test;
        Visit(node.Test);

        if (_areEqual)
        {
            _comparand = other.IfTrue;
            Visit(node.IfTrue);
        }

        if (_areEqual)
        {
            _comparand = other.IfFalse;
            Visit(node.IfFalse);
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        if (_comparand is not NewExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Constructor != other.Constructor)
        {
            _areEqual = false;
            return node;
        }

        if (node.Arguments.Count != other.Arguments.Count)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;

        for (int i = 0; i < node.Arguments.Count && _areEqual; i++)
        {
            _comparand = other.Arguments[i];
            Visit(node.Arguments[i]);
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        if (_comparand is not NewArrayExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Expressions.Count != other.Expressions.Count)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;

        for (int i = 0; i < node.Expressions.Count && _areEqual; i++)
        {
            _comparand = other.Expressions[i];
            Visit(node.Expressions[i]);
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        if (_comparand is not MemberInitExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Bindings.Count != other.Bindings.Count)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;
        _comparand = other.NewExpression;
        Visit(node.NewExpression);

        if (_areEqual)
        {
            for (int i = 0; i < node.Bindings.Count && _areEqual; i++)
            {
                var binding = node.Bindings[i];
                var otherBinding = other.Bindings[i];

                if (binding.Member != otherBinding.Member || binding.BindingType != otherBinding.BindingType)
                {
                    _areEqual = false;
                }
            }
        }

        _comparand = savedComparand;
        return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        if (_comparand is not ListInitExpression other)
        {
            _areEqual = false;
            return node;
        }

        if (node.Initializers.Count != other.Initializers.Count)
        {
            _areEqual = false;
            return node;
        }

        var savedComparand = _comparand;
        _comparand = other.NewExpression;
        Visit(node.NewExpression);

        if (_areEqual)
        {
            for (int i = 0; i < node.Initializers.Count && _areEqual; i++)
            {
                var init = node.Initializers[i];
                var otherInit = other.Initializers[i];

                if (init.Arguments.Count != otherInit.Arguments.Count)
                {
                    _areEqual = false;
                    break;
                }
            }
        }

        _comparand = savedComparand;
        return node;
    }
}
