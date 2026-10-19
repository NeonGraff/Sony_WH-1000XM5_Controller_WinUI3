using System;
using System.ComponentModel;
using System.Globalization;

namespace SonyHeadphoneWH.Services
{
    public enum AppLanguage
    {
        Japanese,
        English
    }

    public class LocalizationService : INotifyPropertyChanged
    {
        private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        private AppLanguage _currentLanguage;
        public AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnAllPropertiesChanged();
                }
            }
        }

        public bool IsJapanese => CurrentLanguage == AppLanguage.Japanese;
        public bool IsEnglish => CurrentLanguage == AppLanguage.English;
        public string LanguageButtonText => IsJapanese ? "English" : "日本語";

        public LocalizationService()
        {
            // Auto detect from OS UI Culture
            string cultureName = CultureInfo.CurrentUICulture.Name;
            _currentLanguage = cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Japanese
                : AppLanguage.English;
        }

        public void ToggleLanguage()
        {
            CurrentLanguage = IsJapanese ? AppLanguage.English : AppLanguage.Japanese;
        }

        protected void OnAllPropertiesChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        // --- Header ---
        public string AppTitle => IsJapanese ? "Sony Headphones コントローラー" : "Sony Headphones Controller";
        public string AppSubtitle => "for Windows";
        public string DeviceSelectPlaceholder => IsJapanese ? "ヘッドホンを選択..." : "Select headphone...";
        public string RefreshDevicesTooltip => IsJapanese ? "状態を更新 / デバイスを再検索" : "Refresh status / Scan devices";
        public string Connect => IsJapanese ? "接続" : "Connect";
        public string Disconnect => IsJapanese ? "切断" : "Disconnect";
        public string PowerOff => IsJapanese ? "電源オフ" : "Power Off";
        public string PowerOffTooltip => IsJapanese ? "ヘッドホンの電源をオフにする" : "Turn off headphone power";

        // --- Left Column: Status ---
        public string Connected => IsJapanese ? "接続中" : "Connected";
        public string Disconnected => IsJapanese ? "未接続" : "Disconnected";
        public string BatteryLevel => IsJapanese ? "バッテリー残量" : "Battery Level";
        public string Charging => IsJapanese ? "充電中" : "Charging";
        public string FirmwareLabel => IsJapanese ? "ファームウェア" : "Firmware";
        public string CodecLabel => IsJapanese ? "接続コーデック" : "Audio Codec";
        public string DseeLabel => "DSEE";
        public string DseeOff => IsJapanese ? "オフ" : "Off";
        public string DseeActive => IsJapanese ? "有効 (アップスケーリング中)" : "Active (Upscaling)";
        public string DseeBypass => IsJapanese ? "バイパス (ハイレゾ伝送中)" : "Bypassed (Hi-Res input)";

        // --- Tabs ---
        public string SoundControlTab => IsJapanese ? "サウンド コントロール" : "Sound Control";
        public string ConnectedDevicesTab => IsJapanese ? "接続デバイス (マルチポイント)" : "Connected Devices (Multipoint)";
        public string ConnectedPrefix => IsJapanese ? "接続中:" : "Connected:";
        public string UnitDevices => IsJapanese ? "台" : "";

        // --- Tab 0: Sound Control ---
        public string AmbientSoundControl => IsJapanese ? "外音コントロール" : "Ambient Sound Control";
        public string NoiseCancelling => IsJapanese ? "ノイズキャンセリング" : "Noise Cancelling";
        public string AmbientSound => IsJapanese ? "外音取り込み" : "Ambient Sound";
        public string Off => IsJapanese ? "オフ" : "Off";
        public string AmbientLevelLabel => IsJapanese ? "取り込みレベル:" : "Ambient Level:";
        public string VoiceFocusHeader => IsJapanese ? "ボイスフォーカス (人の声を強調)" : "Voice Focus (Emphasize voice)";
        public string HeadphoneVolume => IsJapanese ? "ヘッドホン音量" : "Headphone Volume";
        public string VolumeDownTooltip => IsJapanese ? "音量を下げる" : "Volume Down";
        public string VolumeUpTooltip => IsJapanese ? "音量を上げる" : "Volume Up";
        public string NowPlaying => IsJapanese ? "再生中のメディア" : "Now Playing";
        public string RefreshTrackTooltip => IsJapanese ? "曲情報を更新" : "Refresh track info";
        public string PreviousTrackTooltip => IsJapanese ? "前の曲" : "Previous";
        public string NextTrackTooltip => IsJapanese ? "次の曲" : "Next";
        public string PlayPauseTooltip => IsJapanese ? "再生 / 一時停止" : "Play / Pause";
        public string NoTrackTitle => IsJapanese ? "トラック情報なし" : "No track info";
        public string StoppedArtist => IsJapanese ? "停止中 または メディア未再生" : "Stopped or media inactive";

        // --- Tab 1: Connected Devices & Multipoint ---
        public string MultipointHeaderTitle => IsJapanese ? "登録・接続中のデバイス (マルチポイント)" : "Registered & Connected Devices (Multipoint)";
        public string MultipointHeaderSubtitle => IsJapanese
            ? "ペアリング済みのデバイス一覧です。2台の機器と同時に接続し、音声を切り替えることができます。"
            : "Device list paired with headphones. Connect to 2 devices and switch audio output.";
        public string RefreshPairedDevicesButton => IsJapanese ? "再読み込み" : "Refresh";
        public string RefreshPairedDevicesTooltip => IsJapanese ? "デバイス一覧を再取得" : "Refresh device list";
        public string MultipointDisabledTitle => IsJapanese ? "マルチポイント（2台同時接続）が無効です" : "Multipoint (connect to 2 devices) is disabled";
        public string MultipointDisabledMessage => IsJapanese
            ? "アプリ「Sony Sound Connect」の設定で「2台の機器と同時に接続」をオンにすることで、ペアリング済み機器の管理が可能になります。"
            : "Enable 'Connect to 2 devices simultaneously' in the Sony Sound Connect mobile app to manage paired devices.";
        public string EmptyDevicesTitle => IsJapanese ? "マルチポイント機器が見つかりません" : "No multipoint devices found";
        public string EmptyDevicesSubtitle => IsJapanese
            ? "ヘッドホンに機器をペアリングしてください。"
            : "Pair devices with your headphones.";
        public string AudioOutputBadge => IsJapanese ? "再生優先" : "Audio Output";
        public string SwitchAudioButton => IsJapanese ? "音声切替" : "Switch Audio";
        public string SwitchAudioTooltip => IsJapanese ? "この機器の音声をヘッドホンへ出力します" : "Switch headphone audio output to this device";
        public string UnpairTooltip => IsJapanese ? "ヘッドホンからこの機器のペアリングを解除します" : "Unpair this device from headphones";

        // --- Status Bar & Logs ---
        public string ReadyStatus => IsJapanese ? "準備完了" : "Ready";
        public string LogHistoryButton => IsJapanese ? "ログ履歴" : "Log History";
        public string LogHistoryTooltip => IsJapanese ? "通信ログパネルの表示 / 非表示" : "Show / Hide communication log panel";
        public string LogPanelTitle => IsJapanese ? "通信ログ ＆ イベント履歴" : "Communication Logs & Events";
        public string ClearLogsButton => IsJapanese ? "クリア" : "Clear";
        public string ClearLogsTooltip => IsJapanese ? "ログを消去" : "Clear all logs";
        public string CloseLogTooltip => IsJapanese ? "ログパネルを閉じる" : "Close log panel";
    }

    public class LocalizationSource
    {
        public LocalizationService Strings => LocalizationService.Instance;
    }
}

