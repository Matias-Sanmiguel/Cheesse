using Chess.Core.Model;
using NUnit.Framework;

namespace Chess.Tests;

public class ArchitectureTests
{
    [Test]
    public void Core_does_not_reference_godot()
    {
        var names = typeof(ChessGame).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.That(names, Does.Not.Contain("GodotSharp"));
        Assert.That(names.Any(name => name?.Contains("Godot", StringComparison.OrdinalIgnoreCase) == true), Is.False);
    }

    [Test]
    public void Core_source_does_not_mention_godot_types()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "chess-core")))
            dir = dir.Parent;

        Assert.That(dir, Is.Not.Null);
        var sources = Directory.GetFiles(Path.Combine(dir!.FullName, "chess-core"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        var text = string.Join('\n', sources.Select(File.ReadAllText));

        Assert.That(text, Does.Not.Contain("Vector2"));
        Assert.That(text, Does.Not.Contain("[Signal]"));
        Assert.That(text, Does.Not.Contain("class Node"));
    }
}
