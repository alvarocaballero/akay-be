using System.Reflection;
using Akay.Be.Domain.Primitives;
using NetArchTest.Rules;

namespace Akay.Be.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainShouldNotDependOnOuterLayers()
    {
        TestResult result = Types.InAssembly(typeof(Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Application", "Akay.Be.Infrastructure", "Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ApplicationShouldNotDependOnOuterLayers()
    {
        TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Application"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Infrastructure", "Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void InfrastructureShouldNotDependOnHost()
    {
        TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void DomainEventsMustBeSealed()
    {
        TestResult implementationsResult = Types.InAssembly(typeof(Entity).Assembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        TestResult inheritanceResult = Types.InAssembly(typeof(Entity).Assembly)
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
}
