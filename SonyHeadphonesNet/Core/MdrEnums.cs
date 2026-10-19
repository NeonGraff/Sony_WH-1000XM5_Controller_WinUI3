namespace SonyHeadphonesNet.Core;

/// <summary>
/// MDR Table 1 Command IDs.
/// </summary>
public enum MdrV2T1Command : byte
{
    CONNECT_GET_PROTOCOL_INFO = 0x00,
    CONNECT_RET_PROTOCOL_INFO = 0x01,
    CONNECT_GET_CAPABILITY_INFO = 0x02,
    CONNECT_RET_CAPABILITY_INFO = 0x03,
    CONNECT_GET_DEVICE_INFO = 0x04,
    CONNECT_RET_DEVICE_INFO = 0x05,
    CONNECT_GET_SUPPORT_FUNCTION = 0x06,
    CONNECT_RET_SUPPORT_FUNCTION = 0x07,

    COMMON_GET_STATUS = 0x12,
    COMMON_RET_STATUS = 0x13,
    COMMON_NTFY_STATUS = 0x15,

    POWER_GET_STATUS = 0x22,
    POWER_RET_STATUS = 0x23,
    POWER_SET_STATUS = 0x24,
    POWER_NTFY_STATUS = 0x25,
    POWER_GET_PARAM = 0x26,
    POWER_RET_PARAM = 0x27,
    POWER_SET_PARAM = 0x28,
    POWER_NTFY_PARAM = 0x29,

    NCASM_GET_PARAM = 0x66,
    NCASM_RET_PARAM = 0x67,
    NCASM_SET_PARAM = 0x68,
    NCASM_NTFY_PARAM = 0x69,

    PLAY_GET_STATUS = 0xA2,
    PLAY_RET_STATUS = 0xA3,
    PLAY_SET_STATUS = 0xA4,
    PLAY_NTFY_STATUS = 0xA5,
    PLAY_GET_PARAM = 0xA6,
    PLAY_RET_PARAM = 0xA7,
    PLAY_SET_PARAM = 0xA8,
    PLAY_NTFY_PARAM = 0xA9,

    AUDIO_GET_PARAM = 0xE6,
    AUDIO_RET_PARAM = 0xE7,
    AUDIO_SET_PARAM = 0xE8,
    AUDIO_NTFY_PARAM = 0xE9
}

public enum AudioInquiredType : byte
{
    CONNECTION_MODE = 0x00,
    UPSCALING = 0x01,
    OUT_OF_RANGE = 0xFF
}

public enum UpscalingSettingValue : byte
{
    OFF = 0x00,
    AUTO = 0x01
}

public enum PowerInquiredType : byte
{
    BATTERY = 0x00,
    LEFT_RIGHT_BATTERY = 0x01,
    CRADLE_BATTERY = 0x02,
    POWER_OFF = 0x03,
    AUTO_POWER_OFF = 0x04,
    AUTO_POWER_OFF_WEARING_DETECTION = 0x05,
    BATTERY_WITH_THRESHOLD = 0x08,
    LR_BATTERY_WITH_THRESHOLD = 0x09,
    OUT_OF_RANGE = 0xFF
}

public enum PowerOffSettingValue : byte
{
    USER_POWER_OFF = 0x01,
    FACTORY_POWER_OFF = 0xFF
}

public enum BatteryChargingStatus : byte
{
    NOT_CHARGING = 0x00,
    CHARGING = 0x01,
    UNKNOWN = 0xFF
}

public enum PlayInquiredType : byte
{
    PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT = 0x01,
    MUSIC_VOLUME = 0x20,
    CALL_VOLUME = 0x21,
    OUT_OF_RANGE = 0xFF
}

public enum PlaybackControl : byte
{
    KEY_OFF = 0x00,
    PAUSE = 0x01,
    TRACK_UP = 0x02,       // Next Track
    TRACK_DOWN = 0x03,     // Previous Track
    GROUP_UP = 0x04,
    GROUP_DOWN = 0x05,
    STOP = 0x06,
    PLAY = 0x07,
    FAST_FORWARD = 0x08,
    FAST_REWIND = 0x09,
    OUT_OF_RANGE = 0xFF
}

public enum PlaybackStatus : byte
{
    UNSETTLED = 0x00,
    PLAY = 0x01,
    PAUSE = 0x02,
    STOP = 0x03,
    OUT_OF_RANGE = 0xFF
}

public enum NcAsmInquiredType : byte
{
    NC_ON_OFF = 0x01,
    MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS = 0x17,
    MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS_NA = 0x19,
    ASM_SEAMLESS = 0x22,
    OUT_OF_RANGE = 0xFF
}

public enum NcAsmMode : byte
{
    NC = 0x00,             // Noise Cancelling
    ASM = 0x01             // Ambient Sound Mode
}

public enum AmbientSoundFocusMode : byte
{
    NORMAL = 0x00,
    VOICE = 0x01           // Focus on Voice
}

/// <summary>
/// MDR Table 2 Command IDs (Peripheral & Extended functions).
/// </summary>
public enum MdrV2T2Command : byte
{
    PERI_GET_STATUS = 0x32,
    PERI_RET_STATUS = 0x33,
    PERI_SET_STATUS = 0x34,
    PERI_NTFY_STATUS = 0x35,
    PERI_GET_PARAM = 0x36,
    PERI_RET_PARAM = 0x37,
    PERI_SET_PARAM = 0x38,
    PERI_NTFY_PARAM = 0x39,
    PERI_SET_EXTENDED_PARAM = 0x3C,
    PERI_NTFY_EXTENDED_PARAM = 0x3D
}

public enum CommonInquiredType : byte
{
    CONCIERGE = 0x00,
    CONNECTION_STATUS = 0x01,
    AUDIO_CODEC = 0x02,
    UPSCALING_EFFECT = 0x03,
    SMART_PHONE_AND_CONNECTED_DEVICE_INFORMATION = 0x07,
    OUT_OF_RANGE = 0xFF
}

public enum AudioCodec : byte
{
    UNSETTLED = 0x00,
    SBC = 0x01,
    AAC = 0x02,
    LDAC = 0x10,
    APT_X = 0x20,
    APT_X_HD = 0x21,
    LC3 = 0x30,
    OTHER = 0xFF
}

public enum UpscalingEffectType : byte
{
    DSEE_HX = 0x00,
    DSEE = 0x01,
    DSEE_HX_AI = 0x02,     // DSEE Extreme
    DSEE_ULTIMATE = 0x03,
    OUT_OF_RANGE = 0xFF
}

public enum UpscalingEffectStatus : byte
{
    OFF = 0x00,
    VALID = 0x01,          // Active / Enabled
    INVALID = 0x02,        // Inactive / Bypassed (e.g. during Hi-Res LDAC)
    OUT_OF_RANGE = 0xFF
}

public enum PeripheralInquiredType : byte
{
    PAIRING_DEVICE_MANAGEMENT_CLASSIC_BT = 0x01,
    PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE = 0x02,
    SOURCE_SWITCH_CONTROL = 0x04,
    OUT_OF_RANGE = 0xFF
}

public enum ConnectivityActionType : byte
{
    DISCONNECT = 0x00,
    CONNECT = 0x01,
    UNPAIR = 0x02,
    OUT_OF_RANGE = 0xFF
}
