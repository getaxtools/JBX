using System.Reflection;
using Jbx.Core.Diagnostics;
using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class ReportableEventShapeTests
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type[] NumericTypes =
    [
        typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long),
        typeof(double), typeof(TimeSpan), typeof(DateTimeOffset), typeof(Guid),
    ];

    [Fact]
    public void App_events_have_no_member_that_can_hold_text()
    {
        var violations = new List<string>();

        CollectTextCapableFields(typeof(AppEvent), nameof(AppEvent), [], violations);

        Assert.Empty(violations);
    }

    private static void CollectTextCapableFields(Type type, string route, HashSet<Type> visited, List<string> violations)
    {
        if (type.IsEnum || NumericTypes.Contains(type) || !visited.Add(type))
        {
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            CollectTextCapableFields(type.GetGenericArguments()[0], route + "[]", visited, violations);
            return;
        }

        if (type.Namespace?.StartsWith("Jbx.", StringComparison.Ordinal) != true)
        {
            violations.Add($"{route}: {type.FullName}");
            return;
        }

        foreach (var field in type.GetFields(InstanceFields))
        {
            CollectTextCapableFields(field.FieldType, $"{route}.{field.Name}", visited, violations);
        }
    }
}
