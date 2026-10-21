// fork: the app is Flick wherever people read its name. Upstream's strings say "Filee"; LocalizationService runs every
// loaded string through Apply, so the i18n files stay as upstream ships them and new upstream strings follow along.

using System.Text.RegularExpressions;

namespace Filee.App.Services;

public static partial class Brand
{
    public const string Name = "Flick";

    /// <summary>
    /// "Filee" as a word becomes "Flick". File and type names ("Filee.exe", "FileeRelease") and URL paths
    /// (".../KnifeLemon/Filee") stay. Letters of other scripts may touch the word ("Fileeは").
    /// </summary>
    public static string Apply(string text) => UpstreamName().Replace(text, Name);

    [GeneratedRegex(@"(?<![A-Za-z0-9_/])Filee(?![A-Za-z0-9_]|\.[A-Za-z])")]
    private static partial Regex UpstreamName();
}
