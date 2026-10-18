using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Support;

internal static class TestUsers
{
    public static readonly Guid Admin = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    public static readonly Guid Sensei = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    public static readonly Guid Murid = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    public static readonly Guid InactiveMurid = Guid.Parse("a0000000-0000-4000-8000-000000000009");

    public static string TokenFor(string role) => role switch
    {
        Roles.Admin => TestJwt.For(Admin, Roles.Admin),
        Roles.Sensei => TestJwt.For(Sensei, Roles.Sensei),
        Roles.Murid => TestJwt.For(Murid, Roles.Murid),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
