using SonyHeadphonesNet.Core;
using SonyHeadphonesNet.Devices.Models;
using SonyHeadphonesNet.Transport;

namespace SonyHeadphonesNet.Devices;

/// <summary>
/// Device controller implementation specifically for Sony WH-1000XM5 headphones.
/// Communicates over MDR V2 Table 1 protocol.
/// </summary>
public sealed class Wh1000Xm5Device : SonyHeadphoneBase
{
    /// <summary>
    /// Service UUID for WH-1000XM5 (MDR V2).
    /// </summary>
    public static readonly Guid Xm5ServiceUuid = new("956C7B26-D49A-4BA8-B03F-B17D393CB6E2");

    public override Guid ServiceUuid => Xm5ServiceUuid;

    public Wh1000Xm5Device(IBluetoothTransport transport)
        : base(transport, "Sony WH-1000XM5")
    {
    }

    /// <summary>
    /// Factory method using native Windows Winsock RFCOMM Bluetooth transport.
    /// </summary>
    public static Wh1000Xm5Device Create() =>
        new(new WindowsRfcommTransport());

    /// <summary>
    /// Factory method using mock transport for offline UI testing and simulation.
    /// </summary>
    public static Wh1000Xm5Device CreateMock() =>
        new(new MockBluetoothTransport());

    protected override async Task InitializeDeviceAsync(CancellationToken cancellationToken)
    {
        // 1. Handshake: Protocol Info
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.CONNECT_GET_PROTOCOL_INFO, 0x01
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(50, cancellationToken).ConfigureAwait(false);

        // 2. Capabilities
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.CONNECT_GET_CAPABILITY_INFO, 0x01
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(50, cancellationToken).ConfigureAwait(false);

        // 3. Firmware Version query
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.CONNECT_GET_DEVICE_INFO, 0x02 // FW_VERSION
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(40, cancellationToken).ConfigureAwait(false);

        // 4. Model Name query
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.CONNECT_GET_DEVICE_INFO, 0x01 // MODEL_NAME
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(40, cancellationToken).ConfigureAwait(false);

        // 4. Query current states
        await RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        // Query Battery
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.POWER_GET_STATUS, (byte)PowerInquiredType.BATTERY
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Playback Status
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.PLAY_GET_STATUS, (byte)PlayInquiredType.PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Volume
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.PLAY_GET_PARAM, (byte)PlayInquiredType.MUSIC_VOLUME
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Track Info (Title, Album, Artist)
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.PLAY_GET_PARAM, (byte)PlayInquiredType.PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query NC/ASM Settings
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.NCASM_GET_PARAM, (byte)NcAsmInquiredType.MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Codec & DSEE
        await RefreshCodecAndDseeAsync(cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Multipoint Paired Devices
        await RefreshPairedDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task RefreshCodecAndDseeAsync(CancellationToken cancellationToken = default)
    {
        // Query Audio Codec (COMMON_GET_STATUS, AUDIO_CODEC)
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.COMMON_GET_STATUS,
            (byte)CommonInquiredType.AUDIO_CODEC
        }, cancellationToken).ConfigureAwait(false);

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Query Upscaling Effect / DSEE (COMMON_GET_STATUS, UPSCALING_EFFECT)
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.COMMON_GET_STATUS,
            (byte)CommonInquiredType.UPSCALING_EFFECT
        }, cancellationToken).ConfigureAwait(false);
    }

    public override async Task RefreshPairedDevicesAsync(CancellationToken cancellationToken = default)
    {
        // Query Multipoint Paired Devices (Table 2: PERI_GET_PARAM, PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE)
        await SendCommandAsync(MdrDataType.DATA_MDR_NO2, new byte[] {
            (byte)MdrV2T2Command.PERI_GET_PARAM,
            (byte)PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE
        }, cancellationToken).ConfigureAwait(false);
    }

    public override async Task ConnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        byte[] macBytes = FormatMacPayload(macAddress);
        var payload = new byte[3 + 17];
        payload[0] = (byte)MdrV2T2Command.PERI_SET_EXTENDED_PARAM;
        payload[1] = (byte)PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE;
        payload[2] = (byte)ConnectivityActionType.CONNECT;
        Array.Copy(macBytes, 0, payload, 3, 17);

        await SendCommandAsync(MdrDataType.DATA_MDR_NO2, payload, cancellationToken).ConfigureAwait(false);
    }

    public override async Task DisconnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        byte[] macBytes = FormatMacPayload(macAddress);
        var payload = new byte[3 + 17];
        payload[0] = (byte)MdrV2T2Command.PERI_SET_EXTENDED_PARAM;
        payload[1] = (byte)PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE;
        payload[2] = (byte)ConnectivityActionType.DISCONNECT;
        Array.Copy(macBytes, 0, payload, 3, 17);

        await SendCommandAsync(MdrDataType.DATA_MDR_NO2, payload, cancellationToken).ConfigureAwait(false);
    }

    public override async Task UnpairDeviceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        byte[] macBytes = FormatMacPayload(macAddress);
        var payload = new byte[3 + 17];
        payload[0] = (byte)MdrV2T2Command.PERI_SET_EXTENDED_PARAM;
        payload[1] = (byte)PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE;
        payload[2] = (byte)ConnectivityActionType.UNPAIR;
        Array.Copy(macBytes, 0, payload, 3, 17);

        await SendCommandAsync(MdrDataType.DATA_MDR_NO2, payload, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SwitchAudioSourceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        byte[] macBytes = FormatMacPayload(macAddress);
        var payload = new byte[2 + 17];
        payload[0] = (byte)MdrV2T2Command.PERI_SET_EXTENDED_PARAM;
        payload[1] = (byte)PeripheralInquiredType.SOURCE_SWITCH_CONTROL;
        Array.Copy(macBytes, 0, payload, 2, 17);

        await SendCommandAsync(MdrDataType.DATA_MDR_NO2, payload, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] FormatMacPayload(string macAddress)
    {
        string normalized = macAddress.Trim().ToUpperInvariant();
        if (normalized.Length == 12 && !normalized.Contains(':'))
        {
            normalized = string.Join(":", Enumerable.Range(0, 6).Select(i => normalized.Substring(i * 2, 2)));
        }

        byte[] asciiBytes = System.Text.Encoding.ASCII.GetBytes(normalized);
        if (asciiBytes.Length != 17)
        {
            Array.Resize(ref asciiBytes, 17);
        }
        return asciiBytes;
    }

    public override async Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
    {
        byte volByte = (byte)Math.Clamp(volume, 0, 255);
        byte[] payload = {
            (byte)MdrV2T1Command.PLAY_SET_PARAM,
            (byte)PlayInquiredType.MUSIC_VOLUME,
            volByte
        };

        await SendCommandAsync(MdrDataType.DATA_MDR, payload, cancellationToken).ConfigureAwait(false);
        Volume = volByte;
    }

    public override async Task SendTrackControlAsync(PlaybackControl control, CancellationToken cancellationToken = default)
    {
        byte[] payload = {
            (byte)MdrV2T1Command.PLAY_SET_STATUS,
            (byte)PlayInquiredType.PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT,
            0x00, // ENABLE
            (byte)control
        };

        await SendCommandAsync(MdrDataType.DATA_MDR, payload, cancellationToken).ConfigureAwait(false);

        // When track changes or playback state toggles, query the new track info after a brief delay
        if (control is PlaybackControl.TRACK_UP or PlaybackControl.TRACK_DOWN or PlaybackControl.PLAY or PlaybackControl.PAUSE)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                await RefreshTrackInfoAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }
    }

    public override async Task RefreshTrackInfoAsync(CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(MdrDataType.DATA_MDR, new byte[] {
            (byte)MdrV2T1Command.PLAY_GET_PARAM, (byte)PlayInquiredType.PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT
        }, cancellationToken).ConfigureAwait(false);
    }

    protected override void OnPlaybackStatusUpdated()
    {
        _ = RefreshTrackInfoAsync();
    }

    public override async Task SetNoiseControlAsync(NcAsmSettings settings, CancellationToken cancellationToken = default)
    {
        byte totalEffect = (byte)(settings.Enabled ? 0x01 : 0x00);
        byte mode = (byte)settings.Mode;
        byte voiceFocus = (byte)(settings.FocusOnVoice ? 0x01 : 0x00);
        byte level = (byte)Math.Clamp(settings.AmbientSoundLevel, 0, 20);

        byte[] payload = {
            (byte)MdrV2T1Command.NCASM_SET_PARAM,
            (byte)NcAsmInquiredType.MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS,
            0x01, // CHANGED
            totalEffect,
            mode,
            voiceFocus,
            level
        };

        await SendCommandAsync(MdrDataType.DATA_MDR, payload, cancellationToken).ConfigureAwait(false);
        NoiseControl = settings;
    }

    public override async Task PowerOffAsync(CancellationToken cancellationToken = default)
    {
        byte[] payload = {
            (byte)MdrV2T1Command.POWER_SET_STATUS,
            (byte)PowerInquiredType.POWER_OFF,
            (byte)PowerOffSettingValue.USER_POWER_OFF
        };

        await SendCommandAsync(MdrDataType.DATA_MDR, payload, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SetDseeEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        byte[] payload = {
            (byte)MdrV2T1Command.AUDIO_SET_PARAM,
            (byte)AudioInquiredType.UPSCALING,
            enabled ? (byte)UpscalingSettingValue.AUTO : (byte)UpscalingSettingValue.OFF
        };

        await SendCommandAsync(MdrDataType.DATA_MDR, payload, cancellationToken).ConfigureAwait(false);

        // Immediate query for status update
        _ = Task.Run(async () =>
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            await RefreshCodecAndDseeAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }
}
