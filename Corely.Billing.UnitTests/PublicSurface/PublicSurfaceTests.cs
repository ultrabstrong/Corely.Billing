using System.Runtime.CompilerServices;
using Corely.Billing.Services;
using Corely.Billing.Telemetry;

namespace Corely.Billing.UnitTests.PublicSurface;

public class PublicSurfaceTests
{
    private const string FIX =
        "Make it internal, or add it to the allowed list with the reason a host needs it by name.";

    private static readonly System.Reflection.Assembly _assembly = typeof(IGrantService).Assembly;

    private static readonly Type[] _allowed = [typeof(BillingMetricNames)];

    [Fact]
    public void NoEntityProcessorMapperOrRepo_IsPublic()
    {
        var leaked = PublicTypes.InternalByNaming(_assembly);

        Assert.True(leaked.Count == 0, $"{Names(leaked)}. {FIX}");
    }

    [Fact]
    public void EveryPublicType_IsReachableFromThePublicApi()
    {
        var unreachable = PublicTypes.Unreachable(_assembly, IsRoot, _allowed);

        Assert.True(unreachable.Count == 0, $"{Names(unreachable)}. {FIX}");
    }

    [Fact]
    public void ServicesAndOptions_AreRoots()
    {
        Assert.True(IsRoot(typeof(IGrantService)));
        Assert.True(IsRoot(typeof(BillingOptions)));
        Assert.True(IsRoot(typeof(ServiceRegistrationExtensions)));
    }

    private static bool IsRoot(Type type) =>
        type.IsInterface
        || type.Name.EndsWith("Options")
        || (type.IsAbstract && type.IsSealed && type.IsDefined(typeof(ExtensionAttribute), false));

    private static string Names(IEnumerable<Type> types) =>
        string.Join(", ", types.Select(t => t.FullName));
}
