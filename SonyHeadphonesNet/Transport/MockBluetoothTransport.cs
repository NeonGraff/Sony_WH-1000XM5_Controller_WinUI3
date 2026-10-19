using SonyHeadphonesNet.Core;

namespace SonyHeadphonesNet.Transport;

/// <summary>
/// Mock Bluetooth transport for testing and simulation without requiring physical hardware.
/// Emulates WH-1000XM5 responses and state changes.
/// </summary>
public sealed class MockBluetoothTransport : IBluetoothTransport
{
    private bool _isConnected;
    private byte _currentVolume = 15;
    private PlaybackStatus _playbackStatus = PlaybackStatus.PAUSE;
    private bool _ncEnabled = true;
    private NcAsmMode _ncMode = NcAsmMode.NC;
    private byte _ambientLevel = 10;
    private byte _batteryLevel = 85;
    private AudioCodec _currentCodec = AudioCodec.LDAC;
    private UpscalingEffectStatus _currentDseeStatus = UpscalingEffectStatus.VALID;

    private readonly List<SimulatedDevice> _mockDevices = new()
    {
        new("00:1A:7D:DA:71:11", "Windows 11 PC", true, true, 0x0100),
        new("00:1B:DC:08:92:44", "Pixel 8 Pro", true, false, 0x0200),
        new("00:1C:B3:09:85:22", "iPad Air", false, false, 0x0200)
    };

    private record SimulatedDevice(string Mac, string Name, bool Connected, bool Playback, int Cod)
    {
        public bool Connected { get; set; } = Connected;
        public bool Playback { get; set; } = Playback;
    }

    public bool IsConnected => _isConnected;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<string?>? Disconnected;

    public Task ConnectAsync(string macAddress, Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        _isConnected = true;

        // Simulate initial async notification of protocol & state after connection
        _ = Task.Run(async () =>
        {
            await Task.Delay(200, cancellationToken);
            // Protocol Info Response
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.CONNECT_RET_PROTOCOL_INFO, 0x01, 0x02, 0x01, 0x01
            });

            await Task.Delay(50, cancellationToken);
            // Firmware Version Response ("2.0.2")
            byte[] fwBytes = "2.0.2"u8.ToArray();
            var fwPayload = new List<byte> { (byte)MdrV2T1Command.CONNECT_RET_DEVICE_INFO, 0x02, (byte)fwBytes.Length };
            fwPayload.AddRange(fwBytes);
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, fwPayload.ToArray());

            await Task.Delay(50, cancellationToken);
            // Model Name Response ("WH-1000XM5")
            byte[] nameBytes = "WH-1000XM5"u8.ToArray();
            var modelPayload = new List<byte> { (byte)MdrV2T1Command.CONNECT_RET_DEVICE_INFO, 0x01, (byte)nameBytes.Length };
            modelPayload.AddRange(nameBytes);
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, modelPayload.ToArray());

            await Task.Delay(100, cancellationToken);
            // Battery Status (85%, NOT_CHARGING)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.POWER_RET_STATUS, (byte)PowerInquiredType.BATTERY, _batteryLevel, (byte)BatteryChargingStatus.NOT_CHARGING
            });

            await Task.Delay(100, cancellationToken);
            // Playback Status (PAUSE)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.PLAY_RET_STATUS, 0x01, 0x00, (byte)_playbackStatus, 0x00
            });

            await Task.Delay(100, cancellationToken);
            // Volume Status (15)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.PLAY_RET_PARAM, (byte)PlayInquiredType.MUSIC_VOLUME, _currentVolume
            });

            await Task.Delay(100, cancellationToken);
            // NC/ASM Status (NC ON)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.NCASM_RET_PARAM, (byte)NcAsmInquiredType.MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS, 0x01, _ncEnabled ? (byte)1 : (byte)0, (byte)_ncMode, 0x00, _ambientLevel
            });

            await Task.Delay(50, cancellationToken);
            // Audio Codec (LDAC)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.COMMON_RET_STATUS, (byte)CommonInquiredType.AUDIO_CODEC, (byte)_currentCodec
            });

            await Task.Delay(50, cancellationToken);
            // Upscaling Effect / DSEE (DSEE Extreme, VALID)
            SendSimulatedPacket(MdrDataType.DATA_MDR, 0, new byte[] {
                (byte)MdrV2T1Command.COMMON_RET_STATUS, (byte)CommonInquiredType.UPSCALING_EFFECT, (byte)UpscalingEffectType.DSEE_HX_AI, (byte)UpscalingEffectStatus.VALID
            });

            await Task.Delay(50, cancellationToken);
            // Multipoint Paired Devices
            SendMockPairedDevices(0);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        if (_isConnected)
        {
            _isConnected = false;
            Disconnected?.Invoke(this, "Disconnected mock.");
        }
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Mock transport is not connected.");

        if (MdrPacket.TryUnpack(data.Span, out var packet) && packet != null)
        {
            // Always acknowledge received data packet
            if (packet.DataType is MdrDataType.DATA_MDR or MdrDataType.DATA_MDR_NO2)
            {
                byte[] ackFrame = MdrPacket.CreateAck(packet.SequenceNumber);
                DataReceived?.Invoke(this, ackFrame);
            }

            if (packet.DataType == MdrDataType.DATA_MDR_NO2)
            {
                HandleTable2Command(packet.Payload, packet.SequenceNumber);
            }
            else
            {
                HandleClientCommand(packet.Payload, packet.SequenceNumber);
            }
        }

        return Task.CompletedTask;
    }

    private void HandleClientCommand(byte[] payload, byte seq)
    {
        if (payload.Length == 0)
            return;

        var cmd = (MdrV2T1Command)payload[0];
        switch (cmd)
        {
            // Battery Status Query
            case MdrV2T1Command.POWER_GET_STATUS:
                SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                    (byte)MdrV2T1Command.POWER_RET_STATUS, (byte)PowerInquiredType.BATTERY, _batteryLevel, (byte)BatteryChargingStatus.NOT_CHARGING
                });
                break;

            // Power Off
            case MdrV2T1Command.POWER_SET_STATUS:
                if (payload.Length >= 3 && payload[1] == (byte)PowerInquiredType.POWER_OFF)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        await DisconnectAsync();
                    });
                }
                break;

            // Track Control
            case MdrV2T1Command.PLAY_SET_STATUS:
                if (payload.Length >= 4)
                {
                    var control = (PlaybackControl)payload[3];
                    if (control == PlaybackControl.PLAY)
                        _playbackStatus = PlaybackStatus.PLAY;
                    else if (control == PlaybackControl.PAUSE)
                        _playbackStatus = PlaybackStatus.PAUSE;
                    else if (control == PlaybackControl.STOP)
                        _playbackStatus = PlaybackStatus.STOP;

                    // Send Playback Status Notification
                    SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                        (byte)MdrV2T1Command.PLAY_NTFY_STATUS, 0x01, 0x00, (byte)_playbackStatus, 0x00
                    });
                }
                break;

            // Volume Control
            case MdrV2T1Command.PLAY_SET_PARAM:
                if (payload.Length >= 3 && payload[1] == (byte)PlayInquiredType.MUSIC_VOLUME)
                {
                    _currentVolume = payload[2];
                    SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                        (byte)MdrV2T1Command.PLAY_NTFY_PARAM, (byte)PlayInquiredType.MUSIC_VOLUME, _currentVolume
                    });
                }
                break;

            // Volume Query
            case MdrV2T1Command.PLAY_GET_PARAM:
                if (payload.Length >= 2 && payload[1] == (byte)PlayInquiredType.MUSIC_VOLUME)
                {
                    SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                        (byte)MdrV2T1Command.PLAY_RET_PARAM, (byte)PlayInquiredType.MUSIC_VOLUME, _currentVolume
                    });
                }
                break;

            // NC / ASM Settings
            case MdrV2T1Command.NCASM_SET_PARAM:
                if (payload.Length >= 7)
                {
                    _ncEnabled = payload[3] != 0;
                    _ncMode = (NcAsmMode)payload[4];
                    _ambientLevel = payload[6];

                    SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                        (byte)MdrV2T1Command.NCASM_NTFY_PARAM, (byte)NcAsmInquiredType.MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS, 0x01, payload[3], payload[4], payload[5], payload[6]
                    });
                }
                break;

            // NC / ASM Query
            case MdrV2T1Command.NCASM_GET_PARAM:
                SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                    (byte)MdrV2T1Command.NCASM_RET_PARAM, (byte)NcAsmInquiredType.MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS, 0x01, _ncEnabled ? (byte)1 : (byte)0, (byte)_ncMode, 0x00, _ambientLevel
                });
                break;

            // Common Status Query (Codec, DSEE)
            case MdrV2T1Command.COMMON_GET_STATUS:
                if (payload.Length >= 2)
                {
                    var inq = (CommonInquiredType)payload[1];
                    if (inq == CommonInquiredType.AUDIO_CODEC)
                    {
                        SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                            (byte)MdrV2T1Command.COMMON_RET_STATUS, (byte)CommonInquiredType.AUDIO_CODEC, (byte)_currentCodec
                        });
                    }
                    else if (inq == CommonInquiredType.UPSCALING_EFFECT)
                    {
                        SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                            (byte)MdrV2T1Command.COMMON_RET_STATUS, (byte)CommonInquiredType.UPSCALING_EFFECT, (byte)UpscalingEffectType.DSEE_HX_AI, (byte)_currentDseeStatus
                        });
                    }
                }
                break;

            // Audio Set Param (DSEE Upscaling)
            case MdrV2T1Command.AUDIO_SET_PARAM:
                if (payload.Length >= 3 && (AudioInquiredType)payload[1] == AudioInquiredType.UPSCALING)
                {
                    _currentDseeStatus = payload[2] != 0 ? UpscalingEffectStatus.VALID : UpscalingEffectStatus.OFF;
                    SendSimulatedPacket(MdrDataType.DATA_MDR, seq, new byte[] {
                        (byte)MdrV2T1Command.COMMON_NTFY_STATUS, (byte)CommonInquiredType.UPSCALING_EFFECT, (byte)UpscalingEffectType.DSEE_HX_AI, (byte)_currentDseeStatus
                    });
                }
                break;
        }
    }

    private void HandleTable2Command(byte[] payload, byte seq)
    {
        if (payload.Length == 0) return;

        var cmd = (MdrV2T2Command)payload[0];
        switch (cmd)
        {
            case MdrV2T2Command.PERI_GET_PARAM:
                SendMockPairedDevices(seq);
                break;

            case MdrV2T2Command.PERI_SET_EXTENDED_PARAM:
                if (payload.Length >= 3)
                {
                    var inqType = (PeripheralInquiredType)payload[1];
                    if (inqType == PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE && payload.Length >= 3 + 17)
                    {
                        var action = (ConnectivityActionType)payload[2];
                        string targetMac = System.Text.Encoding.ASCII.GetString(payload, 3, 17);
                        var dev = _mockDevices.FirstOrDefault(d => d.Mac.Equals(targetMac, StringComparison.OrdinalIgnoreCase));

                        if (action == ConnectivityActionType.CONNECT && dev != null)
                        {
                            dev.Connected = true;
                        }
                        else if (action == ConnectivityActionType.DISCONNECT && dev != null)
                        {
                            dev.Connected = false;
                            dev.Playback = false;
                        }
                        else if (action == ConnectivityActionType.UNPAIR && dev != null)
                        {
                            _mockDevices.Remove(dev);
                        }

                        // Broadcast update
                        SendMockPairedDevices(seq);
                    }
                    else if (inqType == PeripheralInquiredType.SOURCE_SWITCH_CONTROL && payload.Length >= 2 + 17)
                    {
                        string targetMac = System.Text.Encoding.ASCII.GetString(payload, 2, 17);
                        foreach (var d in _mockDevices)
                        {
                            d.Playback = d.Mac.Equals(targetMac, StringComparison.OrdinalIgnoreCase);
                        }
                        SendMockPairedDevices(seq);
                    }
                }
                break;
        }
    }

    private void SendMockPairedDevices(byte seq)
    {
        var payload = new List<byte>
        {
            (byte)MdrV2T2Command.PERI_RET_PARAM,
            (byte)PeripheralInquiredType.PAIRING_DEVICE_MANAGEMENT_WITH_BLUETOOTH_CLASS_OF_DEVICE,
            (byte)_mockDevices.Count
        };

        int playbackIndex = -1;
        for (int i = 0; i < _mockDevices.Count; i++)
        {
            var dev = _mockDevices[i];
            if (dev.Playback) playbackIndex = i;

            // MAC (17 ASCII bytes)
            byte[] macBytes = System.Text.Encoding.ASCII.GetBytes(dev.Mac.PadRight(17));
            payload.AddRange(macBytes);

            // Connected (1 byte)
            payload.Add(dev.Connected ? (byte)1 : (byte)0);

            // CoD (3 bytes)
            payload.Add((byte)((dev.Cod >> 16) & 0xFF));
            payload.Add((byte)((dev.Cod >> 8) & 0xFF));
            payload.Add((byte)(dev.Cod & 0xFF));

            // Name length + UTF8
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(dev.Name);
            payload.Add((byte)nameBytes.Length);
            payload.AddRange(nameBytes);
        }

        // Playback right index
        payload.Add((byte)(playbackIndex >= 0 ? playbackIndex : 0));

        SendSimulatedPacket(MdrDataType.DATA_MDR_NO2, seq, payload.ToArray());
    }

    private void SendSimulatedPacket(MdrDataType type, byte seq, byte[] payload)
    {
        byte[] frame = MdrPacket.Pack(type, seq, payload);
        DataReceived?.Invoke(this, frame);
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
