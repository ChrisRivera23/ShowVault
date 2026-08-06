namespace ShowVault.Platform.Organizations;

public sealed class Organization
{
    private Organization(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public string Slug { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Organization Create(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedName = name.Trim();
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Name cannot exceed 200 characters.");
        }

        if (!SlugRules.IsValid(normalizedSlug))
        {
            throw new ArgumentException(
                "Slug must contain lowercase letters, numbers, and single hyphens only.",
                nameof(slug));
        }

        return new Organization(Guid.NewGuid(), normalizedName, normalizedSlug, DateTimeOffset.UtcNow);
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Name cannot exceed 200 characters.");
        }

        Name = normalizedName;
    }
}
