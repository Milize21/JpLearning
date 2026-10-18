namespace Nomax.ArchitectureTests;

internal static class RepositoryRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Nomax.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Nomax.slnx was not found above the test output directory.");
    }
}
