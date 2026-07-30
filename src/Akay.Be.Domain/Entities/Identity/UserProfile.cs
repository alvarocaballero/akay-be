using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Identity;

public sealed class UserProfile : Entity<int>, IAuditable
{
    private UserProfile() { }

    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
    public string Language { get; private set; } = default!;
    public bool DarkMode { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
#pragma warning restore S1144

    public static UserProfile Create(int userId,
                                     string language,
                                     bool darkMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (language.Length > 5)
            throw new ArgumentException("Language must be at most 5 characters.", nameof(language));

        return new UserProfile
        {
            UserId = userId,
            Language = language,
            DarkMode = darkMode
        };
    }

    public void UpdateLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (language.Length > 5)
            throw new ArgumentException("Language must be at most 5 characters.", nameof(language));

        Language = language;
    }

    public void UpdateDarkMode(bool darkMode) => DarkMode = darkMode;
}
