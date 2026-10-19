namespace SonyHeadphonesNet.Core;

/// <summary>
/// MDR Packet Data Type enumeration.
/// </summary>
public enum MdrDataType : byte
{
    DATA = 0,
    ACK = 1,
    DATA_MC_NO1 = 2,
    DATA_ICD = 9,
    DATA_EV = 10,
    DATA_MDR = 12,        // 0x0C: Standard Table 1 data command
    DATA_COMMON = 13,
    DATA_MDR_NO2 = 14,    // 0x0E: Table 2 data command
    SHOT = 16,
    SHOT_MC_NO1 = 18,
    SHOT_ICD = 25,
    SHOT_EV = 26,
    SHOT_MDR = 28,
    SHOT_COMMON = 29,
    SHOT_MDR_NO2 = 30,
    LARGE_DATA_COMMON = 45,
    UNKNOWN = 0xFF
}
