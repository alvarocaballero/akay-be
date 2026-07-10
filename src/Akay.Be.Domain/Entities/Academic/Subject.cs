using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class Subject : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<SubjectCenter> _centers = [];
    private readonly List<SubjectAdmin> _admins = [];

    private Subject() { }

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<SubjectCenter> Centers => _centers.AsReadOnly();
    public IReadOnlyCollection<SubjectAdmin> Admins => _admins.AsReadOnly();

    public static Subject Create(string name, string? description, IEnumerable<int> centerIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var centers = (centerIds ?? []).ToList();

        if (centers.Count == 0)
            throw new ArgumentException("A subject must belong to at least one center.", nameof(centerIds));

        if (centers.Any(id => id <= 0))
            throw new ArgumentException("All center IDs must be greater than zero.", nameof(centerIds));

        var subject = new Subject
        {
            Name = name,
            Description = description
        };

        foreach (var centerId in centers)
        {
            subject._centers.Add(SubjectCenter.Create(centerId));
        }

        return subject;
    }

    public void AddCenter(int centerId)
    {
        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        if (_centers.Any(c => c.CenterId == centerId && c.DeletedAt == null))
            throw new InvalidOperationException($"Center {centerId} is already associated with this subject.");

        _centers.Add(SubjectCenter.Create(centerId));
    }

    public void RemoveCenter(int centerId)
    {
        var center = _centers.FirstOrDefault(c => c.CenterId == centerId && c.DeletedAt == null)
            ?? throw new InvalidOperationException($"Center {centerId} is not associated with this subject.");

        if (DeletedAt == null && _centers.Count(c => c.DeletedAt == null) <= 1)
            throw new InvalidOperationException("Cannot remove the last center from an active subject.");

        center.SoftDelete();
    }

    public void AddAdmin(int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero.", nameof(userId));

        if (_admins.Any(a => a.UserId == userId && a.DeletedAt == null))
            throw new InvalidOperationException($"User {userId} is already an admin of this subject.");

        _admins.Add(SubjectAdmin.Create(Id, userId));
    }

    public void RemoveAdmin(int userId)
    {
        var admin = _admins.FirstOrDefault(a => a.UserId == userId && a.DeletedAt == null)
            ?? throw new InvalidOperationException($"User {userId} is not an admin of this subject.");

        admin.SoftDelete();
    }

    public void ChangeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void ChangeDescription(string? description)
    {
        Description = description;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
