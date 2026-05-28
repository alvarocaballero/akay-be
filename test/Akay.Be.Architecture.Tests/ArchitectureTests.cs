using System.Reflection;
using Akay.Be.Domain.Primitives;
using NetArchTest.Rules;

namespace Akay.Be.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainShouldNotDependOnOuterLayers()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(typeof(Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Application", "Akay.Be.Infrastructure", "Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ApplicationShouldNotDependOnOuterLayers()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Application"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Infrastructure", "Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void InfrastructureShouldNotDependOnHost()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void DomainEventsMustBeSealed()
    {
        NetArchTest.Rules.TestResult implementationsResult = Types.InAssembly(typeof(Entity).Assembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        NetArchTest.Rules.TestResult inheritanceResult = Types.InAssembly(typeof(Entity).Assembly)
            .That()
            .Inherit(typeof(DomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(implementationsResult.IsSuccessful);
        Assert.True(inheritanceResult.IsSuccessful);
    }

    [Fact]
    public void DomainEntitiesMustNotHavePublicConstructors()
    {
        IEnumerable<Type> entityTypes = typeof(Entity).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(Entity)));

        foreach (Type entityType in entityTypes)
        {
            bool hasPublicConstructor = entityType.GetConstructors()
                .Any(constructor => constructor.IsPublic);

            Assert.False(hasPublicConstructor, $"{entityType.FullName} has a public constructor.");
        }
    }

    [Fact]
    public void CreateClient_Must_Use_HttpClientNames_Constants()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == "HttpClientNames.cs")
                continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("CreateClient(\"", StringComparison.Ordinal) && !line.Contains("HttpClientNames", StringComparison.Ordinal))
                {
                    violations.Add($"{file}:{i + 1} -> {line.Trim()}");
                }
            }
        }

        Assert.Empty(violations);
    }
}
