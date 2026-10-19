using SonyHeadphonesNet.Core;

namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// Noise Cancelling / Ambient Sound settings.
/// </summary>
public record NcAsmSettings(
    bool Enabled,
    NcAsmMode Mode,
    int AmbientSoundLevel = 20,
    bool FocusOnVoice = false
)
{
    public bool IsNoiseCancelling => Enabled && Mode == NcAsmMode.NC;
    public bool IsAmbientSound => Enabled && Mode == NcAsmMode.ASM;
    public bool IsOff => !Enabled;

    public static NcAsmSettings NoiseCancelling() =>
        new(Enabled: true, Mode: NcAsmMode.NC, AmbientSoundLevel: 0, FocusOnVoice: false);

    public static NcAsmSettings AmbientSound(int level = 20, bool focusOnVoice = false) =>
        new(Enabled: true, Mode: NcAsmMode.ASM, AmbientSoundLevel: Math.Clamp(level, 0, 20), FocusOnVoice: focusOnVoice);

    public static NcAsmSettings Off() =>
        new(Enabled: false, Mode: NcAsmMode.NC, AmbientSoundLevel: 0, FocusOnVoice: false);

    public override string ToString()
    {
        if (!Enabled)
            return "NC/ASM Off";
        if (Mode == NcAsmMode.NC)
            return "Noise Cancelling (ON)";

        string voicePart = FocusOnVoice ? " [Voice Focus]" : "";
        return $"Ambient Sound (Level {AmbientSoundLevel}/20){voicePart}";
    }
}
