using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Hangline.Shell;
using Xunit;

namespace Hangline.Tests;

/// <summary>
/// The port of the upstream <c>check-strings.swift</c>: every string the code
/// asks for must exist in every language, or the UI silently shows English.
/// </summary>
public class LocalizationTests
{
    private static readonly Regex Call = new(@"\bT(?:Format)?\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hangline.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Hangline.sln not found above " + AppContext.BaseDirectory);
    }

    private static HashSet<string> Keys(string resx) =>
        XDocument.Load(resx).Root!.Elements("data")
            .Select(e => (string?)e.Attribute("name") ?? "")
            .Where(n => n.Length > 0)
            .ToHashSet();

    private static IEnumerable<(string File, string Key)> Used()
    {
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Call.Matches(File.ReadAllText(file)))
            {
                string key = match.Groups[1].Value.Replace("\\\"", "\"");
                if (key.Length > 0) yield return (Path.GetFileName(file), key);
            }
        }
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        data.Add("Strings.resx");
        data.Add("Strings.zh-Hans.resx");
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_string_the_code_asks_for_is_translated(string resource)
    {
        HashSet<string> present = Keys(Path.Combine(Root(), "src/Hangline/Resources", resource));
        List<string> missing = Used().Select(u => u.Key).Distinct().Where(k => !present.Contains(k)).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0, $"{resource} is missing {missing.Count} string(s):\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void The_code_actually_asks_for_something() =>
        Assert.NotEmpty(Used().ToList());

    [Fact]
    public void Chinese_is_resolved_for_a_key_that_exists_in_both()
    {
        Localization.SetLanguage("zh-Hans");
        Assert.Equal("把绳子放下来", Localization.T("Show line"));
        Assert.True(Localization.IsChinese);
    }

    [Fact]
    public void English_falls_back_to_the_key_itself_rather_than_a_blank()
    {
        Localization.SetLanguage("en");
        Assert.Equal("This string is not in any catalogue", Localization.T("This string is not in any catalogue"));
    }

    [Fact]
    public void Placeholders_survive_translation()
    {
        Localization.SetLanguage("zh-Hans");
        Assert.Equal("25 张", Localization.TFormat("{0} photos", 25));
    }
}
