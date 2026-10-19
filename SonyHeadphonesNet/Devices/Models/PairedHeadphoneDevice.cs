namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// Bluetooth device paired with and managed by the Sony headphone (Multipoint connection).
/// </summary>
public record PairedHeadphoneDevice(
    string MacAddress,
    string DeviceName,
    bool IsConnected,
    bool IsPlaybackDevice = false,
    int ClassOfDevice = 0
)
{
    /// <summary>
    /// Friendly category icon hint based on Bluetooth Class of Device (CoD).
    /// </summary>
    public string DeviceCategory => (ClassOfDevice & 0x1F00) switch
    {
        0x0100 => "パソコン",
        0x0200 => "スマートフォン",
        0x0400 => "オーディオ機器",
        _ => "Bluetooth 機器"
    };

    public string Glyph => (ClassOfDevice & 0x1F00) switch
    {
        0x0100 => "\uE7F8", // PC / Laptop
        0x0200 => "\uE8EA", // Phone
        0x0400 => "\uE7F6", // Audio / Speaker
        _ => "\uE702"       // Generic Bluetooth
    };

    public override string ToString() =>
        $"{DeviceName} ({MacAddress})" + (IsConnected ? " [接続中]" : " [未接続]") + (IsPlaybackDevice ? " (再生中)" : "");
}
