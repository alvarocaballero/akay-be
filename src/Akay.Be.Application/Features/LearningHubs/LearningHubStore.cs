namespace Akay.Be.Application.Features.LearningHubs;

internal static class LearningHubStore
{
    private static readonly List<LearningHubData> Items =
    [
        new(1, "Academia Newton", "Centro especializado en ciencias y matemáticas", "Calle Mayor 12, Madrid", "Ciencias", "active", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1)),
        new(2, "Instituto Cervantes", "Formación en idiomas y humanidades", "Avenida de la Cultura 45, Barcelona", "Idiomas", "active", DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-3)),
        new(3, "Centro Tecnológico Turing", "Bootcamps de programación y data science", "Calle Innovación 8, Valencia", "Tecnología", "active", DateTime.UtcNow.AddDays(-90), DateTime.UtcNow),
    ];

    private static int _nextId = 4;
    private static readonly Lock Lock = new();

    public static IReadOnlyList<LearningHubData> GetAll() =>
        Items.AsReadOnly();

    public static LearningHubData? GetById(int id) =>
        Items.Find(h => h.Id == id);

    public static LearningHubData Add(LearningHubData data)
    {
        lock (Lock)
        {
            var item = data with { Id = _nextId++, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            Items.Add(item);
            return item;
        }
    }

    public static bool Update(LearningHubData data)
    {
        var index = Items.FindIndex(h => h.Id == data.Id);
        if (index < 0)
            return false;

        Items[index] = data with { UpdatedAt = DateTime.UtcNow };
        return true;
    }

    public static bool Delete(int id) =>
        Items.RemoveAll(h => h.Id == id) > 0;
}

internal sealed record LearningHubData(
    int Id,
    string Name,
    string Description,
    string Address,
    string Category,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
