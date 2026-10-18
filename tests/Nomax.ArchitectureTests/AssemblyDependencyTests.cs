extern alias gateway;

using System.Reflection;
using NetArchTest.Rules;
using Nomax.BuildingBlocks.Hosting;
using Nomax.SharedKernel;
using GatewayHost = gateway::Nomax.Gateway.GatewayHost;

namespace Nomax.ArchitectureTests;

/// <summary>
/// docs/02 §6.1 at IL level, for dependencies a project reference cannot show (framework packages).
/// </summary>
public sealed class AssemblyDependencyTests
{
    private static readonly string[] Infrastructure =
        ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis", "System.Net.Http"];

    [Fact]
    public void SharedKernel_is_plain_dotnet()
    {
        ShouldNotDependOn(
            typeof(Roles).Assembly,
            [.. Infrastructure, "FluentValidation", "Nomax.BuildingBlocks", "Nomax.Modules", "Nomax.Api", "Nomax.Gateway"]);
    }

    [Fact]
    public void BuildingBlocks_does_not_know_modules_or_hosts()
    {
        ShouldNotDependOn(typeof(NomaxApiDefaults).Assembly, ["Nomax.Modules", "Nomax.Api", "Nomax.Gateway"]);
    }

    [Fact]
    public void Gateway_does_not_know_modules_or_the_api()
    {
        ShouldNotDependOn(typeof(GatewayHost).Assembly, ["Nomax.Modules", "Nomax.Api"]);
    }

    [Fact]
    public void Module_contracts_are_plain_dotnet()
    {
        foreach (var contracts in ModuleAssemblies().Where(assembly => IsContracts(assembly.GetName().Name!)))
        {
            ShouldNotDependOn(contracts, [.. Infrastructure, "FluentValidation", "Nomax.BuildingBlocks"]);
        }
    }

    // Business rules (BR-*) live in Domain/ as pure code: no EF, no HTTP (AGENTS.md §4).
    [Fact]
    public void Module_domain_code_is_pure()
    {
        foreach (var module in ModuleAssemblies().Where(assembly => !IsContracts(assembly.GetName().Name!)))
        {
            var result = Types.InAssembly(module)
                .That().ResideInNamespaceContaining(".Domain")
                .ShouldNot().HaveDependencyOnAny([.. Infrastructure, "FluentValidation"])
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(module, result));
        }
    }

    // Modules reach the test output through the Nomax.Api project reference, so new modules are covered automatically.
    private static IEnumerable<Assembly> ModuleAssemblies() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Nomax.Modules.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)));

    private static bool IsContracts(string assemblyName) => assemblyName.EndsWith(".Contracts", StringComparison.Ordinal);

    private static void ShouldNotDependOn(Assembly assembly, string[] namespaces)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(namespaces).GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(assembly, result));
    }

    private static string Describe(Assembly assembly, NetArchTest.Rules.TestResult result) =>
        $"{assembly.GetName().Name}: {string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? [])}";
}
