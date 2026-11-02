namespace Akay.Be.Domain.Entities.Identity;

public sealed class UserProfile
{
    private UserProfile() { }

    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
    public string? Language { get; private set; }
    public bool? DarkMode { get; private set; }

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
