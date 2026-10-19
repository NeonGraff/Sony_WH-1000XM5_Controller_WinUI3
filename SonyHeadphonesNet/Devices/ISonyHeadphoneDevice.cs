using SonyHeadphonesNet.Core;
using SonyHeadphonesNet.Devices.Models;

namespace SonyHeadphonesNet.Devices;

/// <summary>
/// Universal interface for controlling Sony headphones and earbuds.
/// Provides high-level asynchronous operations and reactive events.
/// </summary>
public interface ISonyHeadphoneDevice : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Basic device hardware and connection information.
    /// </summary>
    DeviceInfo DeviceInfo { get; }

    /// <summary>
    /// Current battery level and charging state.
    /// </summary>
    BatteryInfo? Battery { get; }

    /// <summary>
    /// Current playback status and metadata.
    /// </summary>
    PlaybackInfo? Playback { get; }

    /// <summary>
    /// Current media volume (typically 0-30 or 0-255).
    /// </summary>
    int? Volume { get; }

    /// <summary>
    /// Current Noise Cancelling / Ambient Sound settings.
    /// </summary>
    NcAsmSettings? NoiseControl { get; }

    /// <summary>
    /// Currently negotiated Bluetooth audio codec (e.g. SBC, AAC, LDAC).
    /// </summary>
    AudioCodec? CurrentCodec { get; }

    /// <summary>
    /// Current DSEE upscaling effect and operational status.
    /// </summary>
    DseeInfo? Dsee { get; }

    /// <summary>
    /// Devices paired with and managed by the headphone for multipoint connection.
    /// </summary>
    IReadOnlyList<PairedHeadphoneDevice> PairedDevices { get; }

    /// <summary>
    /// Whether the device is connected and initialized.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets or sets whether track metadata should be automatically polled during playback.
    /// Default is true.
    /// </summary>
    bool AutoTrackPollingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the polling interval for track metadata during playback.
    /// Default is 2.5 seconds.
    /// </summary>
    TimeSpan TrackPollingInterval { get; set; }

    /// <summary>
    /// Fired when battery level or charging status changes.
    /// </summary>
    event EventHandler<BatteryInfo>? BatteryChanged;

    /// <summary>
    /// Fired when playback status (Play/Pause/Stop) or track info changes.
    /// </summary>
    event EventHandler<PlaybackInfo>? PlaybackChanged;

    /// <summary>
    /// Fired when volume level changes.
    /// </summary>
    event EventHandler<int>? VolumeChanged;

    /// <summary>
    /// Fired when Noise Cancelling / Ambient Sound settings change.
    /// </summary>
    event EventHandler<NcAsmSettings>? NoiseControlChanged;

    /// <summary>
    /// Fired when device info (model name, firmware version) is updated.
    /// </summary>
    event EventHandler<DeviceInfo>? DeviceInfoUpdated;

    /// <summary>
    /// Fired when audio codec changes.
    /// </summary>
    event EventHandler<AudioCodec>? CodecChanged;

    /// <summary>
    /// Fired when DSEE upscaling status changes.
    /// </summary>
    event EventHandler<DseeInfo>? DseeChanged;

    /// <summary>
    /// Fired when the headphone's paired/connected devices list changes.
    /// </summary>
    event EventHandler<IReadOnlyList<PairedHeadphoneDevice>>? PairedDevicesChanged;

    /// <summary>
    /// Fired when device connection state changes.
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Connects to the headphone using its Bluetooth MAC address.
    /// </summary>
    Task ConnectAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the headphone.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Queries and refreshes all current statuses (Battery, Playback, Volume, NC/ASM, Codec, DSEE, Paired Devices).
    /// </summary>
    Task RefreshStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries and refreshes current track info (Title, Artist, Album).
    /// </summary>
    Task RefreshTrackInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries and refreshes codec and DSEE upscaling statuses.
    /// </summary>
    Task RefreshCodecAndDseeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries and refreshes multipoint paired devices on the headphone.
    /// </summary>
    Task RefreshPairedDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the headphone to connect to a specific paired Bluetooth device.
    /// </summary>
    Task ConnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the headphone to disconnect a specific paired Bluetooth device.
    /// </summary>
    Task DisconnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the headphone to unpair/delete a specific Bluetooth device.
    /// </summary>
    Task UnpairDeviceAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches audio output source to the specified paired device.
    /// </summary>
    Task SwitchAudioSourceAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets headphone music playback volume.
    /// </summary>
    Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a playback control command (Play, Pause, Next Track, Prev Track, Stop).
    /// </summary>
    Task SendTrackControlAsync(PlaybackControl control, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures Noise Cancelling and Ambient Sound Mode.
    /// </summary>
    Task SetNoiseControlAsync(NcAsmSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables DSEE upscaling effect.
    /// </summary>
    Task SetDseeEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Powers off the headphone.
    /// </summary>
    Task PowerOffAsync(CancellationToken cancellationToken = default);
}
