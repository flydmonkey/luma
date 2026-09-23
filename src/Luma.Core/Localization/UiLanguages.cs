namespace Luma.Core.Localization;

public static class UiLanguages
{
    public const string System = "system";
    public const string English = "en";
    public const string SimplifiedChinese = "zh-Hans";
    public const string TraditionalChinese = "zh-Hant";
    public const string Japanese = "ja";
    public const string Korean = "ko";
    public const string En = English;
    public const string ZhHans = SimplifiedChinese;
    public const string ZhHant = TraditionalChinese;
    public const string Ja = Japanese;
    public const string Ko = Korean;

    public static readonly string[] Choices =
    [
        System, English, SimplifiedChinese, TraditionalChinese, Japanese, Korean
    ];

    public static string Normalize(string? value)
    {
        return value switch
        {
            English or SimplifiedChinese or TraditionalChinese or Japanese or Korean or System => value,
            "zh-CN" or "zh" => SimplifiedChinese,
            "zh-TW" or "zh-HK" => TraditionalChinese,
            _ => System
        };
    }

    public static string ResolveEffective(string preference, string? osLanguage = null)
    {
        if (preference != System)
        {
            return Normalize(preference) == System ? English : preference;
        }

        var os = osLanguage ?? global::System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (os.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || os.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
            || os.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase))
        {
            return TraditionalChinese;
        }

        if (os.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return SimplifiedChinese;
        }

        if (os.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return Japanese;
        }

        if (os.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return Korean;
        }

        if (os.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return English;
    }

    public static string FrameworkTag(string resolved) => resolved switch
    {
        English => "en-US",
        SimplifiedChinese => "zh-CN",
        TraditionalChinese => "zh-TW",
        Japanese => "ja-JP",
        Korean => "ko-KR",
        _ => "en-US"
    };
}
