using SonyHeadphonesNet.Core;

namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// DSEE (Digital Sound Enhancement Engine) upscaling state and type.
/// </summary>
public record DseeInfo(
    UpscalingEffectType Type,
    UpscalingEffectStatus Status
)
{
    public bool IsActive => Status == UpscalingEffectStatus.VALID;
    public bool IsDisabled => Status == UpscalingEffectStatus.OFF;
    public bool IsBypassed => Status == UpscalingEffectStatus.INVALID;

    public string DisplayName => Type switch
    {
        UpscalingEffectType.DSEE_HX_AI => "DSEE Extreme",
        UpscalingEffectType.DSEE_HX => "DSEE HX",
        UpscalingEffectType.DSEE_ULTIMATE => "DSEE Ultimate",
        _ => "DSEE"
    };

    public string StatusText => Status switch
    {
        UpscalingEffectStatus.VALID => "有効 (動作中)",
        UpscalingEffectStatus.INVALID => "待機中 (ハイレゾ等で休止)",
        _ => "オフ"
    };

    public override string ToString() => $"{DisplayName}: {StatusText}";
}
