using Akay.Be.Domain.Entities.Academic;

namespace Akay.Be.Application.Features.Topics;

internal static class TopicOrdering
{
    public static void InsertAt(IEnumerable<Topic> topics, int displayOrder)
    {
        foreach (var topic in topics
                     .Where(candidate => candidate.DisplayOrder >= displayOrder)
                     .OrderByDescending(candidate => candidate.DisplayOrder))
        {
            topic.ChangeDisplayOrder(topic.DisplayOrder + 1);
        }
    }

    public static TopicOrderChange MoveTo(IReadOnlyCollection<Topic> topics,
                                          Topic topic,
                                          int displayOrder)
    {
        var ordered = topics
            .OrderBy(candidate => candidate.DisplayOrder)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        var currentPosition = ordered.FindIndex(candidate => candidate.Id == topic.Id);
        if (currentPosition < 0)
            return TopicOrderChange.TopicNotFound;

        if (displayOrder < 1 || displayOrder > ordered.Count)
            return TopicOrderChange.InvalidDisplayOrder;

        if (currentPosition == displayOrder - 1)
            return TopicOrderChange.Unchanged;

        ordered.RemoveAt(currentPosition);
        ordered.Insert(displayOrder - 1, topic);
        ApplySequentialOrder(ordered);

        return TopicOrderChange.Changed;
    }

    public static void Remove(IEnumerable<Topic> topics, Topic topic)
    {
        foreach (var candidate in topics
                     .Where(candidate => candidate.Id != topic.Id && candidate.DisplayOrder > topic.DisplayOrder)
                     .OrderBy(candidate => candidate.DisplayOrder)
                     .ThenBy(candidate => candidate.Id))
        {
            candidate.ChangeDisplayOrder(candidate.DisplayOrder - 1);
        }
    }

    private static void ApplySequentialOrder(IReadOnlyList<Topic> topics)
    {
        for (var index = 0; index < topics.Count; index++)
        {
            topics[index].ChangeDisplayOrder(index + 1);
        }
    }
}

internal enum TopicOrderChange
{
    Changed,
    Unchanged,
    TopicNotFound,
    InvalidDisplayOrder
}
