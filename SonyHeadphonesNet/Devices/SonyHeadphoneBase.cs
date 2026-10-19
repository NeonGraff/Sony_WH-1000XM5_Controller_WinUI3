using System.Text;
using SonyHeadphonesNet.Core;
using SonyHeadphonesNet.Devices.Models;
using SonyHeadphonesNet.Transport;

namespace SonyHeadphonesNet.Devices;

/// <summary>
/// Abstract base class implementing the core Sony MDR protocol state machine,
/// packet deframing, automatic ACK replies, and notification dispatching.
/// </summary>
public abstract class SonyHeadphoneBase : ISonyHeadphoneDevice
{
    protected readonly IBluetoothTransport Transport;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly List<byte> _rxBuffer = new();
    private Timer? _trackPollingTimer;
    private bool _autoTrackPollingEnabled = true;
    private TimeSpan _trackPollingInterval = TimeSpan.FromSeconds(2.5);
    private byte _txSeq;
    private bool _disposed;

    public DeviceInfo DeviceInfo { get; protected set; }
    public BatteryInfo? Battery { get; protected set; }
    public PlaybackInfo? Playback { get; protected set; }
    public int? Volume { get; protected set; }
    public NcAsmSettings? NoiseControl { get; protected set; }
    public AudioCodec? CurrentCodec { get; protected set; }
    public DseeInfo? Dsee { get; protected set; }
    public IReadOnlyList<PairedHeadphoneDevice> PairedDevices { get; protected set; } = Array.Empty<PairedHeadphoneDevice>();

    public bool IsConnected => Transport.IsConnected;

    public bool AutoTrackPollingEnabled
    {
        get => _autoTrackPollingEnabled;
        set
        {
            _autoTrackPollingEnabled = value;
            UpdateTrackPollingState();
        }
    }

    public TimeSpan TrackPollingInterval
    {
        get => _trackPollingInterval;
        set
        {
            _trackPollingInterval = value;
            UpdateTrackPollingState();
        }
    }

    public event EventHandler<BatteryInfo>? BatteryChanged;
    public event EventHandler<PlaybackInfo>? PlaybackChanged;
    public event EventHandler<int>? VolumeChanged;
    public event EventHandler<NcAsmSettings>? NoiseControlChanged;
    public event EventHandler<DeviceInfo>? DeviceInfoUpdated;
    public event EventHandler<AudioCodec>? CodecChanged;
    public event EventHandler<DseeInfo>? DseeChanged;
    public event EventHandler<IReadOnlyList<PairedHeadphoneDevice>>? PairedDevicesChanged;
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Service UUID required for Bluetooth RFCOMM connection.
    /// </summary>
    public abstract Guid ServiceUuid { get; }

    protected SonyHeadphoneBase(IBluetoothTransport transport, string defaultModelName)
    {
        Transport = transport;
        DeviceInfo = new DeviceInfo(defaultModelName, string.Empty);

        Transport.DataReceived += OnTransportDataReceived;
        Transport.Disconnected += OnTransportDisconnected;
    }

    public virtual async Task ConnectAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        _rxBuffer.Clear();
        _txSeq = 0;

        await Transport.ConnectAsync(macAddress, ServiceUuid, cancellationToken).ConfigureAwait(false);
        DeviceInfo = DeviceInfo with { MacAddress = macAddress, IsConnected = true };
        ConnectionStateChanged?.Invoke(this, true);

        // Perform protocol handshake & initial queries
        await InitializeDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task DisconnectAsync()
    {
        await Transport.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Device-specific initialization logic (Handshake, capabilities, model name).
    /// </summary>
    protected abstract Task InitializeDeviceAsync(CancellationToken cancellationToken);

    public abstract Task RefreshStatusAsync(CancellationToken cancellationToken = default);
    public abstract Task RefreshTrackInfoAsync(CancellationToken cancellationToken = default);
    public abstract Task RefreshCodecAndDseeAsync(CancellationToken cancellationToken = default);
    public abstract Task RefreshPairedDevicesAsync(CancellationToken cancellationToken = default);
    public abstract Task ConnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default);
    public abstract Task DisconnectPairedDeviceAsync(string macAddress, CancellationToken cancellationToken = default);
    public abstract Task UnpairDeviceAsync(string macAddress, CancellationToken cancellationToken = default);
    public abstract Task SwitchAudioSourceAsync(string macAddress, CancellationToken cancellationToken = default);
    public abstract Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);
    public abstract Task SendTrackControlAsync(PlaybackControl control, CancellationToken cancellationToken = default);
    public abstract Task SetNoiseControlAsync(NcAsmSettings settings, CancellationToken cancellationToken = default);
    public abstract Task SetDseeEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    public abstract Task PowerOffAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an MDR data payload, wrapping it in an escaped MDR frame with the current sequence number.
    /// </summary>
    protected async Task SendCommandAsync(MdrDataType type, byte[] payload, CancellationToken cancellationToken = default)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte seq = _txSeq;
            _txSeq = (byte)(1 - _txSeq); // Toggle sequence number

            byte[] frame = MdrPacket.Pack(type, seq, payload);
            await Transport.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void OnTransportDataReceived(object? sender, byte[] chunk)
    {
        lock (_rxBuffer)
        {
            _rxBuffer.AddRange(chunk);
            ProcessRxBuffer();
        }
    }

    private void ProcessRxBuffer()
    {
        while (true)
        {
            int startIndex = _rxBuffer.IndexOf(MdrPacket.StartMarker);
            if (startIndex < 0)
            {
                _rxBuffer.Clear();
                return;
            }

            if (startIndex > 0)
            {
                _rxBuffer.RemoveRange(0, startIndex);
            }

            int endIndex = _rxBuffer.IndexOf(MdrPacket.EndMarker);
            if (endIndex < 0)
            {
                // Incomplete frame, wait for more data
                return;
            }

            int frameLength = endIndex + 1;
            byte[] frameBytes = new byte[frameLength];
            _rxBuffer.CopyTo(0, frameBytes, 0, frameLength);
            _rxBuffer.RemoveRange(0, frameLength);

            if (MdrPacket.TryUnpack(frameBytes, out var packet) && packet != null)
            {
                HandleIncomingPacket(packet);
            }
        }
    }

    private void HandleIncomingPacket(MdrUnpackedPacket packet)
    {
        // Auto-reply ACK for incoming data packets
        if (packet.DataType == MdrDataType.DATA_MDR || packet.DataType == MdrDataType.DATA_MDR_NO2)
        {
            byte[] ackFrame = MdrPacket.CreateAck(packet.SequenceNumber);
            _ = Transport.SendAsync(ackFrame);
        }

        if (packet.Payload.Length > 0)
        {
            DispatchCommandPayload(packet.DataType, packet.Payload);
        }
    }

    /// <summary>
    /// Dispatches command payload to specific handlers.
    /// </summary>
    protected virtual void DispatchCommandPayload(MdrDataType type, byte[] payload)
    {
        if (payload.Length == 0) return;

        if (type == MdrDataType.DATA_MDR_NO2)
        {
            var cmd2 = (MdrV2T2Command)payload[0];
            switch (cmd2)
            {
                case MdrV2T2Command.PERI_RET_PARAM:
                case MdrV2T2Command.PERI_NTFY_PARAM:
                    HandlePeripheralParam(payload);
                    break;

                case MdrV2T2Command.PERI_NTFY_EXTENDED_PARAM:
                    _ = RefreshPairedDevicesAsync();
                    break;
            }
            return;
        }

        var cmd = (MdrV2T1Command)payload[0];
        switch (cmd)
        {
            case MdrV2T1Command.CONNECT_RET_DEVICE_INFO:
                HandleDeviceInfo(payload);
                break;

            case MdrV2T1Command.POWER_RET_STATUS:
            case MdrV2T1Command.POWER_NTFY_STATUS:
                HandleBatteryStatus(payload);
                break;

            case MdrV2T1Command.COMMON_RET_STATUS:
            case MdrV2T1Command.COMMON_NTFY_STATUS:
                HandleCommonStatus(payload);
                break;

            case MdrV2T1Command.PLAY_RET_STATUS:
            case MdrV2T1Command.PLAY_NTFY_STATUS:
                HandlePlaybackStatus(payload);
                break;

            case MdrV2T1Command.PLAY_RET_PARAM:
            case MdrV2T1Command.PLAY_NTFY_PARAM:
                HandlePlayParam(payload);
                break;

            case MdrV2T1Command.NCASM_RET_PARAM:
            case MdrV2T1Command.NCASM_NTFY_PARAM:
                HandleNcAsmParam(payload);
                break;
        }
    }

    protected virtual void HandleCommonStatus(byte[] payload)
    {
        // Format: [0x13/0x15, InquiredType, ...]
        if (payload.Length < 2) return;

        var inquiredType = (CommonInquiredType)payload[1];
        switch (inquiredType)
        {
            case CommonInquiredType.AUDIO_CODEC:
                if (payload.Length >= 3)
                {
                    var codec = (AudioCodec)payload[2];
                    CurrentCodec = codec;
                    CodecChanged?.Invoke(this, codec);
                }
                break;

            case CommonInquiredType.UPSCALING_EFFECT:
                if (payload.Length >= 4)
                {
                    var effectType = (UpscalingEffectType)payload[2];
                    var status = (UpscalingEffectStatus)payload[3];
                    Dsee = new DseeInfo(effectType, status);
                    DseeChanged?.Invoke(this, Dsee);
                }
                break;
        }
    }

    protected virtual void HandlePeripheralParam(byte[] payload)
    {
        try
        {
            // Format: [0x37/0x39, InquiredType, count, ...device entries..., playbackRightIndex]
            if (payload.Length < 3) return;

            var inqType = (PeripheralInquiredType)payload[1];
            int count = payload[2];
            int offset = 3;

            var list = new List<PairedHeadphoneDevice>();

            for (int i = 0; i < count && offset < payload.Length; i++)
            {
                // MAC address is 17 bytes ASCII (e.g. "AA:BB:CC:DD:EE:FF")
                if (offset + 17 > payload.Length) break;
                string mac = Encoding.ASCII.GetString(payload, offset, 17);
                offset += 17;

                if (offset >= payload.Length) break;
                byte connStatus = payload[offset++];
                bool isConnected = connStatus != 0;

                int cod = 0;
                if (inqType == PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE)
                {
                    if (offset + 3 > payload.Length) break;
                    cod = (payload[offset] << 16) | (payload[offset + 1] << 8) | payload[offset + 2];
                    offset += 3;
                }

                if (offset >= payload.Length) break;
                int nameLen = payload[offset++];
                string name = string.Empty;
                if (nameLen > 0 && offset + nameLen <= payload.Length)
                {
                    name = Encoding.UTF8.GetString(payload, offset, nameLen);
                    offset += nameLen;
                }

                list.Add(new PairedHeadphoneDevice(mac, name, isConnected, false, cod));
            }

            // The last byte (if present) indicates the index of the device that has playback priority
            if (offset < payload.Length)
            {
                int playbackRightIndex = payload[offset];
                if (playbackRightIndex >= 0 && playbackRightIndex < list.Count)
                {
                    list[playbackRightIndex] = list[playbackRightIndex] with { IsPlaybackDevice = true };
                }
            }

            PairedDevices = list;
            PairedDevicesChanged?.Invoke(this, PairedDevices);
        }
        catch
        {
            // Ignore malformed peripheral payload
        }
    }

    protected virtual void HandleDeviceInfo(byte[] payload)
    {
        // Device info payload format: [0x05, type (0x01:MODEL_NAME, 0x02:FW_VERSION), length, string bytes...]
        if (payload.Length >= 3)
        {
            byte infoType = payload[1];
            int len = payload[2];
            if (payload.Length >= 3 + len)
            {
                string text = Encoding.UTF8.GetString(payload, 3, len);
                if (infoType == 0x01) // MODEL_NAME
                {
                    DeviceInfo = DeviceInfo with { ModelName = text };
                    DeviceInfoUpdated?.Invoke(this, DeviceInfo);
                }
                else if (infoType == 0x02) // FW_VERSION
                {
                    DeviceInfo = DeviceInfo with { FirmwareVersion = text };
                    DeviceInfoUpdated?.Invoke(this, DeviceInfo);
                }
            }
        }
    }

    protected virtual void HandleBatteryStatus(byte[] payload)
    {
        // Format: [0x23/0x25, type, level, chargingStatus]
        if (payload.Length >= 4 && (payload[1] == (byte)PowerInquiredType.BATTERY || payload[1] == (byte)PowerInquiredType.BATTERY_WITH_THRESHOLD))
        {
            int level = payload[2];
            var charging = (BatteryChargingStatus)payload[3];

            Battery = new BatteryInfo(level, charging);
            BatteryChanged?.Invoke(this, Battery);
        }
    }

    protected virtual void HandlePlaybackStatus(byte[] payload)
    {
        // Format: [0xA3/0xA5, 0x01, 0x00, PlaybackStatus, MusicCallStatus]
        if (payload.Length >= 4 && payload[1] == 0x01)
        {
            var status = (PlaybackStatus)payload[3];
            bool statusChanged = Playback == null || Playback.Status != status;
            Playback = (Playback ?? new PlaybackInfo(status)) with { Status = status };

            if (statusChanged)
            {
                PlaybackChanged?.Invoke(this, Playback);
            }

            OnPlaybackStatusUpdated();
            UpdateTrackPollingState();
        }
    }

    protected virtual void OnPlaybackStatusUpdated()
    {
    }

    private void UpdateTrackPollingState()
    {
        if (_disposed || !IsConnected || !_autoTrackPollingEnabled)
        {
            _trackPollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }

        if (Playback?.IsPlaying == true)
        {
            _trackPollingTimer ??= new Timer(OnTrackPollingTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            _trackPollingTimer.Change(_trackPollingInterval, _trackPollingInterval);
        }
        else
        {
            _trackPollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnTrackPollingTimerTick(object? state)
    {
        if (_disposed || !IsConnected || Playback?.IsPlaying != true)
            return;

        _ = RefreshTrackInfoAsync();
    }

    protected virtual void HandlePlayParam(byte[] payload)
    {
        if (payload.Length < 2)
            return;

        var type = (PlayInquiredType)payload[1];
        switch (type)
        {
            case PlayInquiredType.MUSIC_VOLUME:
                if (payload.Length >= 3)
                {
                    Volume = payload[2];
                    VolumeChanged?.Invoke(this, Volume.Value);
                }
                break;

            case PlayInquiredType.PLAYBACK_CONTROL_WITH_CALL_VOLUME_ADJUSTMENT:
                // Track metadata strings (Title, Album, Artist)
                ParseTrackMetadata(payload);
                break;
        }
    }

    protected virtual void ParseTrackMetadata(byte[] payload)
    {
        try
        {
            // Format: [0xA7/0xA9, 0x01, Entry0, Entry1, Entry2, Entry3]
            // Each entry: [PlaybackNameStatus: 1 byte][Length: 1 byte][UTF-8 String: N bytes]
            // Entry 0: Title, Entry 1: Album, Entry 2: Artist
            int offset = 2;
            var strings = new List<string>();
            for (int i = 0; i < 4 && offset < payload.Length; i++)
            {
                byte status = payload[offset++]; // PlaybackNameStatus (0:UNSETTLED, 1:NOTHING, 2:SETTLED)
                if (offset >= payload.Length)
                    break;

                int len = payload[offset++];
                if (offset + len <= payload.Length)
                {
                    string str = len > 0 ? Encoding.UTF8.GetString(payload, offset, len) : string.Empty;
                    strings.Add(str);
                    offset += len;
                }
                else
                {
                    break;
                }
            }

            if (strings.Count >= 3)
            {
                string? title = string.IsNullOrWhiteSpace(strings[0]) ? null : strings[0];
                string? album = string.IsNullOrWhiteSpace(strings[1]) ? null : strings[1];
                string? artist = string.IsNullOrWhiteSpace(strings[2]) ? null : strings[2];

                bool trackChanged = Playback == null ||
                                    Playback.Title != title ||
                                    Playback.Album != album ||
                                    Playback.Artist != artist;

                if (trackChanged)
                {
                    Playback = (Playback ?? new PlaybackInfo(PlaybackStatus.UNSETTLED)) with
                    {
                        Title = title,
                        Album = album,
                        Artist = artist
                    };
                    PlaybackChanged?.Invoke(this, Playback);
                }
            }
        }
        catch
        {
            // Ignore malformed metadata payload
        }
    }

    protected virtual void HandleNcAsmParam(byte[] payload)
    {
        // Format: [0x67/0x69, type, changeStatus, totalEffect, mode, focusOnVoice, level, ...]
        if (payload.Length >= 7)
        {
            bool enabled = payload[3] != 0;
            var mode = (NcAsmMode)payload[4];
            bool voice = payload[5] != 0;
            int level = payload[6];

            NoiseControl = new NcAsmSettings(enabled, mode, level, voice);
            NoiseControlChanged?.Invoke(this, NoiseControl);
        }
    }

    private void OnTransportDisconnected(object? sender, string? reason)
    {
        _trackPollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        DeviceInfo = DeviceInfo with { IsConnected = false };
        ConnectionStateChanged?.Invoke(this, false);
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _trackPollingTimer?.Dispose();
            Transport.DataReceived -= OnTransportDataReceived;
            Transport.Disconnected -= OnTransportDisconnected;
            Transport.Dispose();
            _sendLock.Dispose();
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_trackPollingTimer != null)
            {
                await _trackPollingTimer.DisposeAsync().ConfigureAwait(false);
            }
            Transport.DataReceived -= OnTransportDataReceived;
            Transport.Disconnected -= OnTransportDisconnected;
            await Transport.DisposeAsync().ConfigureAwait(false);
            _sendLock.Dispose();
        }
    }
}
