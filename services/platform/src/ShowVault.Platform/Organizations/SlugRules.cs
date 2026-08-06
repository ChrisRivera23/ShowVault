using System.Text.RegularExpressions;

namespace ShowVault.Platform.Organizations;

internal static partial class SlugRules
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidSlugExpression();

    public static bool IsValid(string value) =>
        value.Length is >= 2 and <= 80 && ValidSlugExpression().IsMatch(value);
}
