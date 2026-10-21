using Luma.Core.Localization;

namespace Luma.Obs;

/// <summary>
/// The native host reports in Simplified Chinese. Known phrases are swapped for the current language;
/// anything else (OBS or driver text) passes through unchanged.
/// </summary>
public static class HostText
{
    private static readonly (string Phrase, string? Key)[] Known =
    [
        ("硬件编码不可用，已回退 obs_x264。", null),
        ("OBS 正常停止超时，已强制结束；成片已通过校验", "stop.forced"),
        ("正在结束 OBS 输出并校验文件…", "stop.pending"),
        ("系统声采集不可用。", "warn.system"),
        ("当前没有麦克风轨道。", "warn.mic.track"),
        ("麦克风不可用。", "warn.mic"),
        ("摄像头不可用。", "warn.camera"),
        ("文字水印不可用。", "warn.text"),
        ("图片水印不可用。", "warn.image"),
        ("时间戳不可用。", "warn.timestamp"),
        ("麦克风降噪不可用。", "warn.denoise"),
        ("水印透明度不可用。", "warn.opacity"),
        ("已有录制正在进行。", "session.busy"),
        ("录制正在结束。", "session.stopping"),
        ("当前没有正在进行的录制。", "session.stop.idle"),
        ("当前输出不支持暂停。", "session.pause.unsupported"),
        ("请先选择窗口。", "target.window"),
        ("游戏录制已关闭。", "target.game"),
        ("无法创建采集源。", "source.create")
    ];

    public static string? Localize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var parts = new List<string>();
        var rest = text;
        foreach (var (phrase, key) in Known)
        {
            if (!rest.Contains(phrase, StringComparison.Ordinal))
            {
                continue;
            }

            rest = rest.Replace(phrase, " ", StringComparison.Ordinal);
            if (key is not null)
            {
                var translated = CoreText.T(key);
                if (!parts.Contains(translated))
                {
                    parts.Add(translated);
                }
            }
        }

        rest = rest.Trim();
        if (rest.Length > 0)
        {
            parts.Add(rest);
        }

        return string.Join(" ", parts);
    }
}
