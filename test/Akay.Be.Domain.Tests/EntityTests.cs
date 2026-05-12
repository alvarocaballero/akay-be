using Akay.Be.Domain.Primitives;

namespace Akay.Be.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void Entity_Should_Be_Abstract()
    {
        Assert.True(typeof(Entity).IsAbstract);
    }

    [Fact]
    public void Entity_Should_Be_Class()
    {
        Assert.True(typeof(Entity).IsClass);
    }

    [Fact]
    public void Entity_Should_Have_Protected_Constructor()
    {
        var constructor = typeof(Entity).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.Contains(constructor, c => c.IsFamily);
    }
}

public sealed class 
    DomainEventTests
{
    [Fact]
    public void DomainEvent_Should_Be_Sealed_Record()
    {
        var type = typeof(DomainEvent);

        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void DomainEvent_Should_Implement_IDomainEvent()
    {
        Assert.True(typeof(IDomainEvent).IsAssignableFrom(typeof(DomainEvent)));
    }
}

public sealed class IDomainEventTests
{
    [Fact]
    public void IDomainEvent_Should_Be_Interface()
    {
        Assert.True(typeof(IDomainEvent).IsInterface);
    }
}
