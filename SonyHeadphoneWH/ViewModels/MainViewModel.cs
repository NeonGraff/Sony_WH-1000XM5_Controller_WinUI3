using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SonyHeadphonesNet;
using SonyHeadphonesNet.Core;
using SonyHeadphonesNet.Devices;
using SonyHeadphonesNet.Devices.Models;
using SonyHeadphoneWH.Services;

namespace SonyHeadphoneWH.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class MainViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly DispatcherQueue _dispatcher;
        private ISonyHeadphoneDevice? _device;
        private CancellationTokenSource? _connectCts;

        // Guard against feedback loops when UI controls update in response to incoming device notifications
        private bool _isUpdatingFromDevice;
        private NcAsmSettings? _lastAppliedNcAsmSettings;
        private int _lastSentVolume = -1;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // --- Log History ---
        public ObservableCollection<string> LogHistory { get; } = new();

        private bool _isLogOpen;
        public bool IsLogOpen
        {
            get => _isLogOpen;
            set
            {
                if (SetProperty(ref _isLogOpen, value))
                {
                    OnPropertyChanged(nameof(LogVisibility));
                }
            }
        }

        public Microsoft.UI.Xaml.Visibility LogVisibility =>
            _isLogOpen ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public void Log(string message)
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timeStr}] {message}";

            _dispatcher.TryEnqueue(() =>
            {
                StatusMessage = message;
                LogHistory.Insert(0, entry);
                // Keep max 200 log items
                while (LogHistory.Count > 200)
                {
                    LogHistory.RemoveAt(LogHistory.Count - 1);
                }
            });
        }

        // --- UI Properties ---

        private string _statusMessage = "準備完了";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotConnected));
                OnPropertyChanged(nameof(ConnectionBadgeText));
                OnPropertyChanged(nameof(ConnectedVisibility));
                OnPropertyChanged(nameof(NotConnectedVisibility));
                OnPropertyChanged(nameof(DeviceImageOpacity));
                OnPropertyChanged(nameof(IsMultipointEnabled));
                OnPropertyChanged(nameof(IsMultipointDisabled));
                OnPropertyChanged(nameof(IsPairedDevicesEmpty));
                OnPropertyChanged(nameof(EmptyPairedDevicesVisibility));
                OnPropertyChanged(nameof(PairedDevicesListVisibility));
            }
        }
        public LocalizationService Strings => LocalizationService.Instance;

        public bool IsNotConnected => !IsConnected;
        public string ConnectionBadgeText => IsConnected ? Strings.Connected : Strings.Disconnected;
        public Visibility ConnectedVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NotConnectedVisibility => IsNotConnected ? Visibility.Visible : Visibility.Collapsed;
        public double DeviceImageOpacity => IsConnected ? 1.0 : 0.6;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _deviceName = "WH-1000XM5";
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        private string _firmwareVersion = "--";
        public string FirmwareVersion
        {
            get => _firmwareVersion;
            set => SetProperty(ref _firmwareVersion, value);
        }

        // Codec & DSEE
        private AudioCodec _currentCodec = AudioCodec.UNSETTLED;
        public AudioCodec CurrentCodec
        {
            get => _currentCodec;
            set
            {
                if (SetProperty(ref _currentCodec, value))
                {
                    OnPropertyChanged(nameof(CodecName));
                    OnPropertyChanged(nameof(IsCodecVisible));
                }
            }
        }
        public string CodecName => CurrentCodec switch
        {
            AudioCodec.LDAC => "LDAC",
            AudioCodec.AAC => "AAC",
            AudioCodec.SBC => "SBC",
            AudioCodec.APT_X => "aptX",
            AudioCodec.APT_X_HD => "aptX HD",
            AudioCodec.LC3 => "LC3",
            _ => "---"
        };
        public bool IsCodecVisible => CurrentCodec != AudioCodec.UNSETTLED;

        private DseeInfo? _dsee;
        public DseeInfo? Dsee
        {
            get => _dsee;
            set
            {
                if (SetProperty(ref _dsee, value))
                {
                    OnPropertyChanged(nameof(DseeDisplayName));
                    OnPropertyChanged(nameof(DseeStatusText));
                    OnPropertyChanged(nameof(IsDseeActive));
                    OnPropertyChanged(nameof(IsDseeEnabled));
                }
            }
        }
        public string DseeDisplayName => Dsee?.DisplayName ?? "DSEE";
        public string DseeStatusText
        {
            get
            {
                if (Dsee == null || Dsee.IsDisabled) return Strings.DseeOff;
                if (Dsee.IsBypassed) return Strings.DseeBypass;
                if (Dsee.IsActive) return Strings.DseeActive;
                return Strings.DseeOff;
            }
        }
        public bool IsDseeActive => Dsee?.IsActive == true;
        public bool IsDseeEnabled => Dsee != null && !Dsee.IsDisabled;

        // Multipoint Paired Devices on the headphone
        public ObservableCollection<PairedHeadphoneDevice> HeadphonePairedDevices { get; } = new();

        public int ConnectedDevicesCount => HeadphonePairedDevices.Count(d => d.IsConnected);
        public bool IsMultipointEnabled => HeadphonePairedDevices.Count > 0;
        public bool IsMultipointDisabled => IsConnected && HeadphonePairedDevices.Count == 0;
        public bool IsPairedDevicesEmpty => HeadphonePairedDevices.Count == 0;
        public Visibility EmptyPairedDevicesVisibility => IsPairedDevicesEmpty ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PairedDevicesListVisibility => IsPairedDevicesEmpty ? Visibility.Collapsed : Visibility.Visible;

        // Right panel tab selection (0: Sound & Media, 1: Connected Devices & Multipoint)
        private int _selectedRightPanelTabIndex = 0;
        public int SelectedRightPanelTabIndex
        {
            get => _selectedRightPanelTabIndex;
            set
            {
                if (SetProperty(ref _selectedRightPanelTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsSoundTabActive));
                    OnPropertyChanged(nameof(IsDeviceTabActive));
                    OnPropertyChanged(nameof(SoundTabVisibility));
                    OnPropertyChanged(nameof(DeviceTabVisibility));
                }
            }
        }
        public bool IsSoundTabActive => SelectedRightPanelTabIndex == 0;
        public bool IsDeviceTabActive => SelectedRightPanelTabIndex == 1;
        public Visibility SoundTabVisibility => IsSoundTabActive ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DeviceTabVisibility => IsDeviceTabActive ? Visibility.Visible : Visibility.Collapsed;

        // Battery
        private int _batteryLevel;
        public int BatteryLevel
        {
            get => _batteryLevel;
            set
            {
                if (_batteryLevel == value) return;
                _batteryLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BatteryGlyph));
            }
        }
        public string BatteryGlyph => BatteryLevel switch
        {
            >= 90 => "\uEBAA",
            >= 75 => "\uEBA8",
            >= 50 => "\uEBA6",
            >= 25 => "\uEBA3",
            >= 10 => "\uEBA1",
            _ => "\uEBA0"
        };

        private bool _isCharging;
        public bool IsCharging
        {
            get => _isCharging;
            set
            {
                if (_isCharging == value) return;
                _isCharging = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChargingVisibility));
            }
        }
        public Visibility ChargingVisibility => IsCharging ? Visibility.Visible : Visibility.Collapsed;

        // Playback & Track
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayPauseIconGlyph));
            }
        }
        // Segoe Fluent Icons: Play = \uE768, Pause = \uE769
        public string PlayPauseIconGlyph => IsPlaying ? "\uE769" : "\uE768";

        private string _trackTitle = "再生中の曲はありません";
        public string TrackTitle
        {
            get => _trackTitle;
            set => SetProperty(ref _trackTitle, value);
        }

        private string _trackArtist = "";
        public string TrackArtist
        {
            get => _trackArtist;
            set => SetProperty(ref _trackArtist, value);
        }

        private string _trackAlbum = "";
        public string TrackAlbum
        {
            get => _trackAlbum;
            set => SetProperty(ref _trackAlbum, value);
        }

        // Volume
        private int _volume;
        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnPropertyChanged();
                }
            }
        }

        // Noise Control
        // 0: Noise Cancelling, 1: Ambient Sound, 2: Off
        private int _selectedNoiseControlModeIndex = 0;
        public int SelectedNoiseControlModeIndex
        {
            get => _selectedNoiseControlModeIndex;
            set
            {
                if (_selectedNoiseControlModeIndex != value)
                {
                    _selectedNoiseControlModeIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAmbientSoundActive));
                    OnPropertyChanged(nameof(AmbientSoundVisibility));
                }
            }
        }
        public bool IsAmbientSoundActive => SelectedNoiseControlModeIndex == 1;
        public Visibility AmbientSoundVisibility => IsAmbientSoundActive ? Visibility.Visible : Visibility.Collapsed;

        private int _ambientSoundLevel = 20;
        public int AmbientSoundLevel
        {
            get => _ambientSoundLevel;
            set
            {
                if (_ambientSoundLevel != value)
                {
                    _ambientSoundLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _voiceFocus;
        public bool VoiceFocus
        {
            get => _voiceFocus;
            set
            {
                if (_voiceFocus != value)
                {
                    _voiceFocus = value;
                    OnPropertyChanged();
                    if (IsConnected && IsAmbientSoundActive && !_isUpdatingFromDevice)
                    {
                        _ = ApplyNoiseControlAsync();
                    }
                }
            }
        }

        // Discovered Devices
        public ObservableCollection<DeviceInfo> DiscoveredDevices { get; } = new();

        private DeviceInfo? _selectedDevice;
        public DeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        // --- Commands ---
        public ICommand ScanDevicesCommand { get; }
        public ICommand RefreshOrScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand NextTrackCommand { get; }
        public ICommand PreviousTrackCommand { get; }
        public ICommand VolumeUpCommand { get; }
        public ICommand VolumeDownCommand { get; }
        public ICommand SetVolumeCommand { get; }
        public ICommand ApplyNoiseControlCommand { get; }
        public ICommand PowerOffCommand { get; }
        public ICommand RefreshTrackCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ToggleLogCommand { get; }
        public ICommand CloseLogCommand { get; }
        public ICommand ToggleLanguageCommand { get; }
        public ICommand ConnectPairedDeviceCommand { get; }
        public ICommand DisconnectPairedDeviceCommand { get; }
        public ICommand UnpairDeviceCommand { get; }
        public ICommand SwitchAudioSourceCommand { get; }
        public ICommand RefreshPairedDevicesCommand { get; }

        public MainViewModel(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;

            ScanDevicesCommand = new AsyncRelayCommand(ScanDevicesAsync);
            RefreshOrScanCommand = new AsyncRelayCommand(RefreshOrScanAsync);
            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
            PlayPauseCommand = new AsyncRelayCommand(TogglePlayPauseAsync);
            NextTrackCommand = new AsyncRelayCommand(NextTrackAsync);
            PreviousTrackCommand = new AsyncRelayCommand(PreviousTrackAsync);
            VolumeUpCommand = new AsyncRelayCommand(() => SetVolumeDeltaAsync(1));
            VolumeDownCommand = new AsyncRelayCommand(() => SetVolumeDeltaAsync(-1));
            SetVolumeCommand = new AsyncRelayCommand(async param =>
            {
                if (param is double d) await SetVolumeAsync((int)d);
                else if (param is int i) await SetVolumeAsync(i);
            });
            ApplyNoiseControlCommand = new AsyncRelayCommand(ApplyNoiseControlAsync);
            PowerOffCommand = new AsyncRelayCommand(PowerOffAsync);
            RefreshTrackCommand = new AsyncRelayCommand(RefreshTrackAsync);
            ClearLogsCommand = new RelayCommand(() => LogHistory.Clear());
            ToggleLogCommand = new RelayCommand(() => IsLogOpen = !IsLogOpen);
            CloseLogCommand = new RelayCommand(() => IsLogOpen = false);
            ToggleLanguageCommand = new RelayCommand(() => Strings.ToggleLanguage());

            Strings.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(ConnectionBadgeText));
                OnPropertyChanged(nameof(DseeStatusText));
            };

            ConnectPairedDeviceCommand = new AsyncRelayCommand(async param =>
            {
                if (param is PairedHeadphoneDevice dev) await ConnectHeadphonePairedDeviceAsync(dev.MacAddress);
                else if (param is string mac) await ConnectHeadphonePairedDeviceAsync(mac);
            });
            DisconnectPairedDeviceCommand = new AsyncRelayCommand(async param =>
            {
                if (param is PairedHeadphoneDevice dev) await DisconnectHeadphonePairedDeviceAsync(dev.MacAddress);
                else if (param is string mac) await DisconnectHeadphonePairedDeviceAsync(mac);
            });
            UnpairDeviceCommand = new AsyncRelayCommand(async param =>
            {
                if (param is PairedHeadphoneDevice dev) await UnpairHeadphoneDeviceAsync(dev.MacAddress);
                else if (param is string mac) await UnpairHeadphoneDeviceAsync(mac);
            });
            SwitchAudioSourceCommand = new AsyncRelayCommand(async param =>
            {
                if (param is PairedHeadphoneDevice dev) await SwitchAudioSourceAsync(dev.MacAddress);
                else if (param is string mac) await SwitchAudioSourceAsync(mac);
            });
            RefreshPairedDevicesCommand = new AsyncRelayCommand(RefreshPairedDevicesAsync);

            Log("アプリケーションが起動しました。");
        }

        public async Task InitializeAsync()
        {
            await ScanDevicesAsync();
        }

        /// <summary>
        /// 接続状態に応じた更新アクション:
        /// - 接続中: ヘッドホンの状態と曲情報を更新
        /// - 未接続: ペアリング済みBluetoothデバイスを再検索
        /// </summary>
        public async Task RefreshOrScanAsync()
        {
            if (IsConnected)
            {
                await RefreshAllStatusAsync();
            }
            else
            {
                await ScanDevicesAsync();
            }
        }

        public async Task ScanDevicesAsync()
        {
            if (IsConnected)
            {
                Log("接続中はデバイスを再検索できません。先に切断してください。");
                return;
            }

            IsBusy = true;
            Log("ペアリング済みの Sony ヘッドホンを検索中...");
            try
            {
                var devices = await Task.Run(() => SonyHeadphoneManager.GetPairedDevices(true).ToList());
                _dispatcher.TryEnqueue(() =>
                {
                    DiscoveredDevices.Clear();
                    foreach (var d in devices)
                    {
                        DiscoveredDevices.Add(d);
                    }
                    if (DiscoveredDevices.Any())
                    {
                        if (SelectedDevice == null || !DiscoveredDevices.Any(d => d.MacAddress == SelectedDevice.MacAddress))
                        {
                            SelectedDevice = DiscoveredDevices.First();
                        }
                        Log($"{DiscoveredDevices.Count} 台の Sony デバイスが見つかりました: {string.Join(", ", DiscoveredDevices.Select(x => x.ModelName))}");
                    }
                    else
                    {
                        Log("WindowsのBluetooth設定にペアリング済みのSonyヘッドホンが見つかりませんでした。");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"検索エラー: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshAllStatusAsync()
        {
            if (_device == null || !IsConnected) return;

            IsBusy = true;
            Log("ヘッドホンの状態を更新中...");
            try
            {
                await _device.RefreshStatusAsync();
                await _device.RefreshTrackInfoAsync();
                await _device.RefreshCodecAndDseeAsync();
                await _device.RefreshPairedDevicesAsync();
                Log("ヘッドホンの状態を正常に更新しました。");
            }
            catch (Exception ex)
            {
                Log($"更新エラー: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConnectAsync()
        {
            if (SelectedDevice == null)
            {
                Log("接続するデバイスを選択してください。");
                return;
            }

            IsBusy = true;
            Log($"{SelectedDevice.ModelName} ({SelectedDevice.MacAddress}) に接続中...");
            _connectCts = new CancellationTokenSource();

            try
            {
                if (_device != null)
                {
                    await _device.DisconnectAsync();
                    UnsubscribeDeviceEvents(_device);
                }

                _device = SonyHeadphoneManager.CreateDevice(SelectedDevice.ModelName);
                SubscribeDeviceEvents(_device);

                await _device.ConnectAsync(SelectedDevice.MacAddress, _connectCts.Token);
                IsConnected = true;
                DeviceName = _device.DeviceInfo.ModelName;
                Log($"{_device.DeviceInfo.ModelName} に接続しました。");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Log($"接続に失敗しました: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisconnectAsync()
        {
            if (_device == null) return;
            IsBusy = true;
            Log("切断中...");
            try
            {
                await _device.DisconnectAsync();
                UnsubscribeDeviceEvents(_device);
                _device = null;
                IsConnected = false;
                Log("ヘッドホンから切断しました。");
            }
            catch (Exception ex)
            {
                Log($"切断エラー: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SubscribeDeviceEvents(ISonyHeadphoneDevice device)
        {
            device.BatteryChanged += Device_BatteryChanged;
            device.PlaybackChanged += Device_PlaybackChanged;
            device.VolumeChanged += Device_VolumeChanged;
            device.NoiseControlChanged += Device_NoiseControlChanged;
            device.DeviceInfoUpdated += Device_DeviceInfoUpdated;
            device.CodecChanged += Device_CodecChanged;
            device.DseeChanged += Device_DseeChanged;
            device.PairedDevicesChanged += Device_PairedDevicesChanged;
            device.ConnectionStateChanged += Device_ConnectionStateChanged;
        }

        private void UnsubscribeDeviceEvents(ISonyHeadphoneDevice device)
        {
            device.BatteryChanged -= Device_BatteryChanged;
            device.PlaybackChanged -= Device_PlaybackChanged;
            device.VolumeChanged -= Device_VolumeChanged;
            device.NoiseControlChanged -= Device_NoiseControlChanged;
            device.DeviceInfoUpdated -= Device_DeviceInfoUpdated;
            device.CodecChanged -= Device_CodecChanged;
            device.DseeChanged -= Device_DseeChanged;
            device.PairedDevicesChanged -= Device_PairedDevicesChanged;
            device.ConnectionStateChanged -= Device_ConnectionStateChanged;
        }

        private void Device_BatteryChanged(object? sender, BatteryInfo e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                BatteryLevel = e.LevelPercent;
                IsCharging = e.IsCharging;
                Log($"バッテリー残量: {e.LevelPercent}% {(e.IsCharging ? "(充電中)" : "")}");
            });
        }

        private void Device_PlaybackChanged(object? sender, PlaybackInfo e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                IsPlaying = e.IsPlaying;
                TrackTitle = string.IsNullOrWhiteSpace(e.Title) ? "再生中の曲はありません" : e.Title;
                TrackArtist = e.Artist ?? "";
                TrackAlbum = e.Album ?? "";
                if (!string.IsNullOrWhiteSpace(e.Title))
                {
                    Log($"再生情報: \"{e.Title}\" - {e.Artist} ({(e.IsPlaying ? "再生中" : "一時停止")})");
                }
            });
        }

        private void Device_VolumeChanged(object? sender, int volume)
        {
            _dispatcher.TryEnqueue(() =>
            {
                _lastSentVolume = volume;
                _volume = volume;
                OnPropertyChanged(nameof(Volume));
                Log($"音量変更: {volume}");
            });
        }

        private void Device_NoiseControlChanged(object? sender, NcAsmSettings e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                try
                {
                    // Set updating flag to prevent UI selection changes from triggering SetNoiseControlAsync back to device
                    _isUpdatingFromDevice = true;
                    _lastAppliedNcAsmSettings = e;

                    if (e.IsOff)
                    {
                        SelectedNoiseControlModeIndex = 2; // Off
                        Log("外音コントロール: オフ");
                    }
                    else if (e.IsNoiseCancelling)
                    {
                        SelectedNoiseControlModeIndex = 0; // NC
                        Log("外音コントロール: ノイズキャンセリング");
                    }
                    else if (e.IsAmbientSound)
                    {
                        SelectedNoiseControlModeIndex = 1; // ASM
                        AmbientSoundLevel = e.AmbientSoundLevel;
                        VoiceFocus = e.FocusOnVoice;
                        Log($"外音コントロール: 外音取り込み (レベル {e.AmbientSoundLevel}, ボイスフォーカス={(e.FocusOnVoice ? "ON" : "OFF")})");
                    }
                }
                finally
                {
                    _isUpdatingFromDevice = false;
                }
            });
        }

        private void Device_DeviceInfoUpdated(object? sender, DeviceInfo e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                DeviceName = e.ModelName;
                FirmwareVersion = string.IsNullOrWhiteSpace(e.FirmwareVersion) ? "--" : $"v{e.FirmwareVersion}";
                Log($"機器情報更新: {e.ModelName}, ファームウェア: {FirmwareVersion}");
            });
        }

        private void Device_CodecChanged(object? sender, AudioCodec codec)
        {
            _dispatcher.TryEnqueue(() =>
            {
                CurrentCodec = codec;
                Log($"オーディオコーデック更新: {CodecName}");
            });
        }

        private void Device_DseeChanged(object? sender, DseeInfo dsee)
        {
            _dispatcher.TryEnqueue(() =>
            {
                Dsee = dsee;
                Log($"DSEE ステータス更新: {dsee}");
            });
        }

        private void Device_PairedDevicesChanged(object? sender, IReadOnlyList<PairedHeadphoneDevice> devices)
        {
            _dispatcher.TryEnqueue(() =>
            {
                HeadphonePairedDevices.Clear();
                foreach (var d in devices)
                {
                    HeadphonePairedDevices.Add(d);
                }
                OnPropertyChanged(nameof(ConnectedDevicesCount));
                OnPropertyChanged(nameof(IsMultipointEnabled));
                OnPropertyChanged(nameof(IsMultipointDisabled));
                OnPropertyChanged(nameof(IsPairedDevicesEmpty));
                OnPropertyChanged(nameof(EmptyPairedDevicesVisibility));
                OnPropertyChanged(nameof(PairedDevicesListVisibility));
                Log($"ヘッドホンのペアリング機器更新 ({devices.Count} 台, 接続中: {devices.Count(x => x.IsConnected)} 台)");
            });
        }

        private void Device_ConnectionStateChanged(object? sender, bool connected)
        {
            _dispatcher.TryEnqueue(() =>
            {
                IsConnected = connected;
                if (!connected)
                {
                    HeadphonePairedDevices.Clear();
                    CurrentCodec = AudioCodec.UNSETTLED;
                    Dsee = null;
                    Log("ヘッドホンとの接続が切断されました。");
                }
            });
        }

        // --- Control Actions ---

        private async Task TogglePlayPauseAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                if (IsPlaying)
                {
                    Log("操作: 一時停止");
                    await _device.SendTrackControlAsync(PlaybackControl.PAUSE);
                }
                else
                {
                    Log("操作: 再生");
                    await _device.SendTrackControlAsync(PlaybackControl.PLAY);
                }
            }
            catch (Exception ex)
            {
                Log($"再生制御エラー: {ex.Message}");
            }
        }

        private async Task NextTrackAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log("操作: 次の曲");
                await _device.SendTrackControlAsync(PlaybackControl.TRACK_UP);
            }
            catch (Exception ex)
            {
                Log($"曲送りエラー: {ex.Message}");
            }
        }

        private async Task PreviousTrackAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log("操作: 前の曲");
                await _device.SendTrackControlAsync(PlaybackControl.TRACK_DOWN);
            }
            catch (Exception ex)
            {
                Log($"曲戻しエラー: {ex.Message}");
            }
        }

        public async Task SetVolumeAsync(int volume, bool force = false)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                volume = Math.Clamp(volume, 0, 30);
                if (!force && _lastSentVolume == volume) return;

                _lastSentVolume = volume;
                await _device.SetVolumeAsync(volume);
                Volume = volume;
                Log($"音量を設定: {volume}");
            }
            catch (Exception ex)
            {
                Log($"音量制御エラー: {ex.Message}");
            }
        }

        private async Task SetVolumeDeltaAsync(int delta)
        {
            await SetVolumeAsync(Volume + delta, force: true);
        }

        public async Task ApplyNoiseControlAsync()
        {
            // CRITICAL: Ignore if the property change was triggered by incoming notification from device!
            if (_isUpdatingFromDevice) return;
            if (_device == null || !IsConnected) return;

            try
            {
                NcAsmSettings newSettings = SelectedNoiseControlModeIndex switch
                {
                    0 => NcAsmSettings.NoiseCancelling(),
                    1 => NcAsmSettings.AmbientSound(AmbientSoundLevel, VoiceFocus),
                    _ => NcAsmSettings.Off()
                };

                // Prevent redundant transmissions if settings have not changed
                if (_lastAppliedNcAsmSettings != null &&
                    _lastAppliedNcAsmSettings.Enabled == newSettings.Enabled &&
                    _lastAppliedNcAsmSettings.Mode == newSettings.Mode &&
                    _lastAppliedNcAsmSettings.AmbientSoundLevel == newSettings.AmbientSoundLevel &&
                    _lastAppliedNcAsmSettings.FocusOnVoice == newSettings.FocusOnVoice)
                {
                    return;
                }

                _lastAppliedNcAsmSettings = newSettings;
                await _device.SetNoiseControlAsync(newSettings);

                string desc = SelectedNoiseControlModeIndex switch
                {
                    0 => "ノイズキャンセリング",
                    1 => $"外音取り込み (レベル {AmbientSoundLevel}, ボイスフォーカス={(VoiceFocus ? "ON" : "OFF")})",
                    _ => "オフ"
                };
                Log($"外音モード適用: {desc}");
            }
            catch (Exception ex)
            {
                Log($"外音コントロール設定エラー: {ex.Message}");
            }
        }

        private async Task PowerOffAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log("ヘッドホンの電源をオフにしています...");
                await _device.PowerOffAsync();
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                Log($"電源オフエラー: {ex.Message}");
            }
        }

        private async Task RefreshTrackAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log("再生情報を取得中...");
                await _device.RefreshTrackInfoAsync();
            }
            catch (Exception ex)
            {
                Log($"再生情報取得エラー: {ex.Message}");
            }
        }

        public async Task RefreshPairedDevicesAsync()
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log("ヘッドホンのペアリング済み機器一覧を取得中...");
                await _device.RefreshPairedDevicesAsync();
            }
            catch (Exception ex)
            {
                Log($"ペアリング機器取得エラー: {ex.Message}");
            }
        }

        public async Task ConnectHeadphonePairedDeviceAsync(string macAddress)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log($"ヘッドホンからデバイス ({macAddress}) へ接続要求を送信中...");
                await _device.ConnectPairedDeviceAsync(macAddress);
            }
            catch (Exception ex)
            {
                Log($"デバイス接続要求エラー: {ex.Message}");
            }
        }

        public async Task DisconnectHeadphonePairedDeviceAsync(string macAddress)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log($"ヘッドホンからデバイス ({macAddress}) の切断要求を送信中...");
                await _device.DisconnectPairedDeviceAsync(macAddress);
            }
            catch (Exception ex)
            {
                Log($"デバイス切断要求エラー: {ex.Message}");
            }
        }

        public async Task UnpairHeadphoneDeviceAsync(string macAddress)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log($"ヘッドホンからデバイス ({macAddress}) のペアリング解除（削除）要求を送信中...");
                await _device.UnpairDeviceAsync(macAddress);
            }
            catch (Exception ex)
            {
                Log($"デバイス削除要求エラー: {ex.Message}");
            }
        }

        public async Task SwitchAudioSourceAsync(string macAddress)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log($"音声出力ソースをデバイス ({macAddress}) に切り替え中...");
                await _device.SwitchAudioSourceAsync(macAddress);
            }
            catch (Exception ex)
            {
                Log($"ソース切り替えエラー: {ex.Message}");
            }
        }

        public async Task SetDseeEnabledAsync(bool enabled)
        {
            if (_device == null || !IsConnected) return;
            try
            {
                Log(enabled ? "DSEEを有効に設定中..." : "DSEEを無効に設定中...");
                await _device.SetDseeEnabledAsync(enabled);
            }
            catch (Exception ex)
            {
                Log($"DSEE設定エラー: {ex.Message}");
            }
        }
    }
}
