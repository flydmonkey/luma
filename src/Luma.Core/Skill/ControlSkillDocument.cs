using System.Net;
using System.Reflection;
using System.Text;

namespace Luma.Core.Skill;

public static class ControlSkillDocument
{
    public static string ReadMarkdown() => ReadEmbedded("Luma.Skill.luma-control.md");

    public static string ReadReference() => ReadEmbedded("Luma.Skill.luma-control.reference.md");

    public static string ToHtml(bool dark)
    {
        var css = dark
            ? "html,body{background:#1c1c1c;color:#f3f3f3;}a{color:#60cdff;}body{font:15px/1.55 'Segoe UI',sans-serif;max-width:48rem;margin:1.5rem auto;padding:0 1.25rem;}pre{white-space:pre-wrap;word-break:break-word;}"
            : "html,body{background:#fafafa;color:#1a1a1a;}a{color:#0067c0;}body{font:15px/1.55 'Segoe UI',sans-serif;max-width:48rem;margin:1.5rem auto;padding:0 1.25rem;}pre{white-space:pre-wrap;word-break:break-word;}";
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>SKILL</title><style>"
            + css
            + "</style></head><body><nav>"
            + "<a href=\"/skill\">/skill</a> · "
            + "<a href=\"/skill/reference.md\">/skill/reference.md</a> · "
            + "<a href=\"/openapi.json\">/openapi.json</a> · "
            + "<a href=\"/api/docs\">/api/docs</a>"
            + "</nav><h1>SKILL</h1><pre>"
            + WebUtility.HtmlEncode(ReadMarkdown())
            + "</pre><h1>/skill/reference.md</h1><pre>"
            + WebUtility.HtmlEncode(ReadReference())
            + "</pre></body></html>";
    }

    private static string ReadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
        {
            return "";
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
