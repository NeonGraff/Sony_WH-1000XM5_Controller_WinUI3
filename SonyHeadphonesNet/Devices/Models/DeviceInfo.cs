namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// Headphone hardware and connection information.
/// </summary>
public record DeviceInfo(
    string ModelName,
    string MacAddress,
    string? FirmwareVersion = null,
    bool IsConnected = false
)
{
    public override string ToString() =>
        $"{ModelName} ({MacAddress})" + (FirmwareVersion != null ? $" [FW {FirmwareVersion}]" : "");
}
