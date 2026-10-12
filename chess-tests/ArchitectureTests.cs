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
}
