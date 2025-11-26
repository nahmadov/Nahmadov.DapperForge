using System.Reflection;

namespace DapperToolkit.Core.Validation;

internal static class EntityValidationMetadata<TEntity> where TEntity : class
{
    public static readonly PropertyValidationMetadata[] Properties = Build();

    private static PropertyValidationMetadata[] Build()
    {
        var type = typeof(TEntity);

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.CanRead &&
                                    p.GetIndexParameters().Length == 0)
                        .Select(p => new PropertyValidationMetadata(p))
                        .Where(m => m.HasAnyRule)
                        .ToArray();

        return props;
    }
}