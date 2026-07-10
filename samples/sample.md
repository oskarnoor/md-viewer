# OpenMd Smoke Test

This file checks that **bold text**, _italic text_, `inline code`, links, lists, tables, task lists, blockquotes, and fenced code render in the preview.

## Checklist

- [x] Open a `.md` file from the command line
- [x] Render a preview
- [ ] Edit and save changes

## Table

| Feature | Expected result |
| --- | --- |
| Headings | Render as document headings |
| Tables | Render as bordered tables |
| Code | Preserve formatting |

> Markdown blockquotes should appear visually distinct from normal body text.

```csharp
public static string Hello(string name)
{
    return $"Hello, {name}!";
}
```

Visit [OpenAI](https://openai.com/) to verify external links open outside the embedded preview.
