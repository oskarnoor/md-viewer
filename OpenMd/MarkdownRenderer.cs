using System.Net;
using Markdig;

namespace OpenMd;

internal static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string ToHtmlDocument(string markdown, string title, string? baseDirectory, AppTheme? selectedTheme = null)
    {
        var theme = selectedTheme ?? AppThemes.Light;
        var body = Markdown.ToHtml(markdown ?? string.Empty, Pipeline);
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedThemeName = WebUtility.HtmlEncode(theme.Name);
        var baseTag = BuildBaseTag(baseDirectory);

        return $$"""
<!doctype html>
<html data-theme="{{encodedThemeName}}">
<head>
  <meta charset="utf-8">
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  {{baseTag}}
  <title>{{encodedTitle}}</title>
  <style>
    html {
      background: {{Hex(theme.PreviewCanvas)}};
      color: {{Hex(theme.Text)}};
      font-family: "Segoe UI", Arial, sans-serif;
      font-size: 16px;
      line-height: 1.55;
    }

    body {
      box-sizing: border-box;
      margin: 0 auto;
      max-width: 980px;
      min-height: 100vh;
      padding: 34px 42px 56px;
      background: {{Hex(theme.PreviewPage)}};
    }

    h1, h2, h3, h4, h5, h6 {
      color: {{Hex(theme.Heading)}};
      font-weight: 650;
      line-height: 1.25;
      margin: 1.35em 0 0.55em;
    }

    h1 {
      border-bottom: 1px solid {{Hex(theme.Border)}};
      font-size: 2.05rem;
      padding-bottom: 0.25em;
    }

    h2 {
      border-bottom: 1px solid {{Hex(theme.SubtleBorder)}};
      font-size: 1.55rem;
      padding-bottom: 0.2em;
    }

    h3 {
      font-size: 1.22rem;
    }

    p, ul, ol, blockquote, table, pre {
      margin-bottom: 1rem;
    }

    a {
      color: {{Hex(theme.Link)}};
      text-decoration: none;
    }

    a:hover {
      text-decoration: underline;
    }

    code {
      background: {{Hex(theme.InlineCodeBack)}};
      border-radius: 4px;
      color: {{Hex(theme.InlineCodeFore)}};
      font-family: Consolas, "Cascadia Mono", monospace;
      font-size: 0.92em;
      padding: 0.12em 0.35em;
    }

    pre {
      background: {{Hex(theme.CodeBlockBack)}};
      border-radius: 6px;
      color: {{Hex(theme.CodeBlockFore)}};
      overflow: auto;
      padding: 16px;
    }

    pre code {
      background: transparent;
      color: inherit;
      display: block;
      padding: 0;
      white-space: pre;
    }

    blockquote {
      border-left: 4px solid {{Hex(theme.QuoteBorder)}};
      color: {{Hex(theme.MutedText)}};
      margin-left: 0;
      padding: 0.1rem 1rem;
    }

    table {
      border-collapse: collapse;
      display: block;
      overflow: auto;
      width: 100%;
    }

    th, td {
      border: 1px solid {{Hex(theme.Border)}};
      padding: 7px 10px;
    }

    th {
      background: {{Hex(theme.TableHeaderBack)}};
      font-weight: 650;
    }

    tr:nth-child(even) td {
      background: {{Hex(theme.TableAltBack)}};
    }

    img {
      height: auto;
      max-width: 100%;
    }

    hr {
      border: 0;
      border-top: 1px solid {{Hex(theme.Border)}};
      margin: 1.8rem 0;
    }

    input[type="checkbox"] {
      margin-right: 0.45rem;
    }
  </style>
</head>
<body>
{{body}}
</body>
</html>
""";
    }

    private static string Hex(Color color) => AppThemes.ToHtmlColor(color);

    private static string BuildBaseTag(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(baseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var uri = new Uri(fullPath);

        return $"""<base href="{WebUtility.HtmlEncode(uri.AbsoluteUri)}">""";
    }
}
