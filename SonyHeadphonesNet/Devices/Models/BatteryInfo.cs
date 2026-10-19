using SonyHeadphonesNet.Core;

namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// Headphone battery and charging information.
/// </summary>
public record BatteryInfo(
    int LevelPercent,
    BatteryChargingStatus ChargingStatus
)
{
    public bool IsCharging => ChargingStatus == BatteryChargingStatus.CHARGING;

    public override string ToString() =>
        $"{LevelPercent}%" + (IsCharging ? " (Charging)" : "");
}
