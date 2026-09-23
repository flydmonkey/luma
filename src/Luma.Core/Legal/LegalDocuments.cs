using System.Reflection;
using System.Text;
using Luma.Core.Localization;

namespace Luma.Core.Legal;

public static class LegalDocuments
{
    public const string Privacy = "privacy";
    public const string Terms = "terms";

    public static string ReadHtml(string document, string? locale)
    {
        var name = string.Equals(document, Terms, StringComparison.OrdinalIgnoreCase) ? Terms : Privacy;
        var lang = UiLanguages.ResolveEffective(string.IsNullOrWhiteSpace(locale) ? UiLanguages.System : locale);
        if (lang == UiLanguages.System)
        {
            lang = UiLanguages.En;
        }

        return ReadResource(lang, name) ?? ReadResource(UiLanguages.En, name) ?? "";
    }

    public static string ApplyTheme(string html, bool dark)
    {
        var css = dark
            ? "html,body{background:#1c1c1c;color:#f3f3f3;}a{color:#60cdff;}code{color:#f3f3f3;}body{font:15px/1.55 'Segoe UI',sans-serif;max-width:40rem;margin:1.5rem auto;padding:0 1.25rem;}"
            : "html,body{background:#fafafa;color:#1a1a1a;}a{color:#0067c0;}body{font:15px/1.55 'Segoe UI',sans-serif;max-width:40rem;margin:1.5rem auto;padding:0 1.25rem;}";
        var inject = "<style>" + css + "</style>";
        var index = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? html.Insert(index, inject) : inject + html;
    }

    private static string? ReadResource(string locale, string document)
    {
        var name = $"Luma.Legal.{locale}.{document}.html";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
