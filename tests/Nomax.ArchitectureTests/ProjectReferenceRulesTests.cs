using System.Xml.Linq;

namespace Nomax.ArchitectureTests;

/// <summary>
/// docs/02 §6.1 at project level: a module can only see SharedKernel, BuildingBlocks and other modules' Contracts,
/// so it cannot even compile against another module's DbContext or internal classes.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
    private const string SharedKernel = "Nomax.SharedKernel";
    private const string BuildingBlocks = "Nomax.BuildingBlocks";

    [Fact]
    public void Every_source_project_follows_the_dependency_table()
    {
        var projects = Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.csproj", SearchOption.AllDirectories);
        projects.ShouldNotBeEmpty();

        var violations = projects
            .SelectMany(project =>
            {
                var name = Path.GetFileNameWithoutExtension(project);
                return ProjectReferences(project)
                    .Where(reference => !IsAllowed(name, reference))
                    .Select(reference => $"{name} → {reference}");
            })
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SharedKernel, BuildingBlocks, false)]
    [InlineData(BuildingBlocks, SharedKernel, true)]
    [InlineData(BuildingBlocks, "Nomax.Modules.Identity", false)]
    [InlineData("Nomax.Gateway", BuildingBlocks, true)]
    [InlineData("Nomax.Gateway", "Nomax.Modules.Identity.Contracts", false)]
    [InlineData("Nomax.Api", "Nomax.Modules.Practice", true)]
    [InlineData("Nomax.Modules.Practice", "Nomax.Modules.Classroom.Contracts", true)]
    [InlineData("Nomax.Modules.Practice", "Nomax.Modules.Practice.Contracts", true)]
    [InlineData("Nomax.Modules.Practice", "Nomax.Modules.Classroom", false)]
    [InlineData("Nomax.Modules.Practice", "Nomax.Api", false)]
    [InlineData("Nomax.Modules.Practice.Contracts", SharedKernel, true)]
    [InlineData("Nomax.Modules.Practice.Contracts", BuildingBlocks, false)]
    [InlineData("Nomax.Modules.Practice.Contracts", "Nomax.Modules.Content.Contracts", false)]
    [InlineData("Nomax.Something.New", SharedKernel, false)]
    public void Rules_match_the_architecture_document(string project, string reference, bool allowed)
    {
        IsAllowed(project, reference).ShouldBe(allowed);
    }

    private static bool IsAllowed(string project, string reference) => project switch
    {
        SharedKernel => false,
        BuildingBlocks => reference == SharedKernel,
        "Nomax.Gateway" => reference is SharedKernel or BuildingBlocks,
        // The composition root loads every module.
        "Nomax.Api" => true,
        _ when IsContracts(project) => reference == SharedKernel,
        _ when IsModule(project) => reference is SharedKernel or BuildingBlocks || IsContracts(reference),
        // Unknown kind of project: extend this table (and docs/02 §6.1) first.
        _ => false,
    };

    private static bool IsModule(string name) => name.StartsWith("Nomax.Modules.", StringComparison.Ordinal) && !IsContracts(name);

    private static bool IsContracts(string name) =>
        name.StartsWith("Nomax.Modules.", StringComparison.Ordinal) && name.EndsWith(".Contracts", StringComparison.Ordinal);

    private static IEnumerable<string> ProjectReferences(string projectFile) =>
        XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)));
}
