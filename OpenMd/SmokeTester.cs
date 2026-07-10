using System.Text;

namespace OpenMd;

internal static class SmokeTester
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return 2;
        }

        var sourcePath = Path.GetFullPath(args[0]);
        var resultPath = Path.ChangeExtension(sourcePath, ".smoke-test.txt");
        var renderedPath = Path.ChangeExtension(sourcePath, ".rendered.html");

        try
        {
            if (!File.Exists(sourcePath))
            {
                File.WriteAllText(resultPath, $"FAIL: File not found: {sourcePath}", Encoding.UTF8);
                return 2;
            }

            var markdown = File.ReadAllText(sourcePath);
            var missing = new List<string>();

            foreach (var theme in AppThemes.All)
            {
                var html = MarkdownRenderer.ToHtmlDocument(markdown, Path.GetFileName(sourcePath), Path.GetDirectoryName(sourcePath), theme);
                var themedRenderedPath = Path.ChangeExtension(sourcePath, $".{SafeThemeName(theme.Name)}.rendered.html");
                File.WriteAllText(themedRenderedPath, html, Encoding.UTF8);

                if (ReferenceEquals(theme, AppThemes.Light))
                {
                    File.WriteAllText(renderedPath, html, Encoding.UTF8);
                }

                var checks = new Dictionary<string, bool>
                {
                    [$"{theme.Name}: theme marker"] = html.Contains($"data-theme=\"{theme.Name}\"", StringComparison.OrdinalIgnoreCase),
                    [$"{theme.Name}: heading"] = html.Contains("<h1", StringComparison.OrdinalIgnoreCase),
                    [$"{theme.Name}: table"] = html.Contains("<table", StringComparison.OrdinalIgnoreCase),
                    [$"{theme.Name}: fenced code"] = html.Contains("<pre><code", StringComparison.OrdinalIgnoreCase),
                    [$"{theme.Name}: blockquote"] = html.Contains("<blockquote", StringComparison.OrdinalIgnoreCase),
                    [$"{theme.Name}: task checkbox"] = html.Contains("checkbox", StringComparison.OrdinalIgnoreCase)
                };

                missing.AddRange(checks.Where(check => !check.Value).Select(check => check.Key));
            }

            if (missing.Count > 0)
            {
                File.WriteAllText(resultPath, $"FAIL: Missing rendered features: {string.Join(", ", missing)}", Encoding.UTF8);
                return 1;
            }

            File.WriteAllText(resultPath, $"PASS: Rendered {sourcePath} with {AppThemes.All.Count} themes. Default output: {renderedPath}", Encoding.UTF8);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(resultPath, $"FAIL: {ex}", Encoding.UTF8);
            return 1;
        }
    }

    private static string SafeThemeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character));
    }
}
