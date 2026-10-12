using System.Globalization;
using System.Resources;

namespace Hangline.Shell;

/// <summary>
/// Localization with the English source text as the key, exactly like the
/// original's <c>L("…")</c>: an untranslated string degrades to English
/// instead of showing a key.
/// </summary>
public static class Localization
{
    private static ResourceManager? _manager;
    private static string _culture = "auto";

    /// "auto", "zh-Hans" or "en".
    public static void SetLanguage(string culture) => _culture = culture;

    public static bool IsChinese => Resolve() == "zh-Hans";

    private static string Resolve()
    {
        if (_culture is "zh-Hans" or "en") return _culture;
        string ui = CultureInfo.CurrentUICulture.Name;
        return ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-Hans" : "en";
    }

    private static ResourceManager Manager =>
        _manager ??= new ResourceManager("Hangline.Resources.Strings", typeof(Localization).Assembly);

    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english)) return english;
        try
        {
            string? found = Manager.GetString(english, CultureInfo.GetCultureInfo(Resolve()));
            if (string.IsNullOrEmpty(found))
            {
                // Falling back to the English text is the right thing to show,
                // but it is silent: the test suite only sees literal calls with
                // a constant string, so a key built from a variable would slip
                // through. Saying it in the log is what catches that.
                if (IsChinese) Log.Write($"missing translation: {english}");
                return english;
            }
            return found;
        }
        catch (MissingManifestResourceException)
        {
            return english;
        }
    }

    /// For strings with a placeholder, e.g. TFormat("{0} photos", 25).
    public static string TFormat(string english, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, T(english), args);

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Resolve());
}
