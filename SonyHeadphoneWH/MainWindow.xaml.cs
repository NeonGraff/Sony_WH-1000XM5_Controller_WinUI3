using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SonyHeadphonesNet.Devices.Models;
using SonyHeadphoneWH.ViewModels;

namespace SonyHeadphoneWH
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        private const int InitialLogicalWidth = 980;
        private const int InitialLogicalHeight = 705;

        private readonly DispatcherTimer _volumeDebounceTimer;
        private readonly DispatcherTimer _ambientDebounceTimer;
        private readonly SUBCLASSPROC _subclassProc;

        public MainWindow()
        {
            this.InitializeComponent();

            // Setup Mica Backdrop if supported
            if (MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop();
            }

            // Initialize ViewModel with Window DispatcherQueue
            ViewModel = new MainViewModel(this.DispatcherQueue);

            // Update Window Title based on language
            UpdateWindowTitle();
            SonyHeadphoneWH.Services.LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                this.DispatcherQueue.TryEnqueue(UpdateWindowTitle);
            };

            // DPI スケーリングを考慮した正確な物理サイズを算出
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindow(hWnd);
            float scale = (dpi > 0) ? (float)dpi / 96f : 1f;
            int physicalWidth = (int)(InitialLogicalWidth * scale);
            int physicalHeight = (int)(InitialLogicalHeight * scale);

            // Set initial window size & disable maximize
            var appWindow = GetAppWindowForCurrentWindow();
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.IsMaximizable = false; // 最大化を無効化
                }
            }

            // 初期サイズ未満に縮小できないよう制限 (WM_GETMINMAXINFO)
            _subclassProc = new SUBCLASSPROC(WndProc);
            SetWindowSubclass(hWnd, _subclassProc, 1, IntPtr.Zero);

            // Set initial selected tab
            TabSelectorBar.SelectedItem = SoundTabItem;

            // Debounce timers to optimize Bluetooth communication traffic
            _volumeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _volumeDebounceTimer.Tick += (s, e) =>
            {
                _volumeDebounceTimer.Stop();
                CommitVolume();
            };

            _ambientDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _ambientDebounceTimer.Tick += (s, e) =>
            {
                _ambientDebounceTimer.Stop();
                CommitAmbientLevel();
            };

            // Trigger initial device scan
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                await ViewModel.InitializeAsync();
            });
        }

        private Microsoft.UI.Windowing.AppWindow? GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
        }

        // --- Volume Slider Optimization ---

        private void OnVolumeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ViewModel == null || !ViewModel.IsConnected) return;

            // While dragging/sliding, reset timer so we don't spam the Bluetooth socket.
            _volumeDebounceTimer.Stop();
            _volumeDebounceTimer.Start();
        }

        private void OnVolumeSliderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // When slider is released, commit immediately!
            _volumeDebounceTimer.Stop();
            CommitVolume();
        }

        private void CommitVolume()
        {
            if (ViewModel != null && ViewModel.IsConnected)
            {
                _ = ViewModel.SetVolumeAsync(ViewModel.Volume, force: true);
            }
        }

        // --- Ambient Sound Slider Optimization ---

        private void OnAmbientLevelValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ViewModel == null || !ViewModel.IsConnected || !ViewModel.IsAmbientSoundActive) return;

            // While dragging/sliding, reset timer.
            _ambientDebounceTimer.Stop();
            _ambientDebounceTimer.Start();
        }

        private void OnAmbientSliderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // When slider is released, commit immediately!
            _ambientDebounceTimer.Stop();
            CommitAmbientLevel();
        }

        private void CommitAmbientLevel()
        {
            if (ViewModel != null && ViewModel.IsConnected && ViewModel.IsAmbientSoundActive)
            {
                _ = ViewModel.ApplyNoiseControlAsync();
            }
        }

        // --- Noise Mode & Voice Focus Controls ---

        private void OnNoiseModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel != null && ViewModel.IsConnected)
            {
                _ = ViewModel.ApplyNoiseControlAsync();
            }
        }

        private void OnVoiceFocusToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ViewModel != null && ViewModel.IsConnected && ViewModel.IsAmbientSoundActive)
            {
                // UI 操作の値を確実に ViewModel に反映させてから即座に適用
                ViewModel.VoiceFocus = ts.IsOn;
                _ = ViewModel.ApplyNoiseControlAsync();
            }
        }

        private void OnDseeToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ViewModel != null && ViewModel.IsConnected)
            {
                if (ViewModel.IsDseeEnabled != ts.IsOn)
                {
                    _ = ViewModel.SetDseeEnabledAsync(ts.IsOn);
                }
            }
        }

        // --- Paired Devices Management Handlers ---

        private void OnConnectPairedDeviceClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PairedHeadphoneDevice device)
            {
                ViewModel.ConnectPairedDeviceCommand.Execute(device);
            }
        }

        private void OnDisconnectPairedDeviceClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PairedHeadphoneDevice device)
            {
                ViewModel.DisconnectPairedDeviceCommand.Execute(device);
            }
        }

        private void OnSwitchAudioSourceClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PairedHeadphoneDevice device)
            {
                ViewModel.SwitchAudioSourceCommand.Execute(device);
            }
        }

        private void OnUnpairDeviceClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PairedHeadphoneDevice device)
            {
                ViewModel.UnpairDeviceCommand.Execute(device);
            }
        }

        private void OnSelectorBarSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (ViewModel != null && sender.SelectedItem != null)
            {
                ViewModel.SelectedRightPanelTabIndex = sender.Items.IndexOf(sender.SelectedItem);
            }
        }

        #region Win32 Minimum Window Size Enforcement

        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_GETMINMAXINFO)
            {
                var minMax = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                uint dpi = GetDpiForWindow(hWnd);
                float scale = (dpi > 0) ? (float)dpi / 96f : 1f;

                // 起動時とまったく同じ物理ピクセルサイズを最小トラックサイズとして指定
                minMax.ptMinTrackSize.x = (int)(InitialLogicalWidth * scale);
                minMax.ptMinTrackSize.y = (int)(InitialLogicalHeight * scale);

                Marshal.StructureToPtr(minMax, lParam, true);
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void UpdateWindowTitle()
        {
            this.Title = SonyHeadphoneWH.Services.LocalizationService.Instance.IsJapanese
                ? "Sony Headphones コントローラー"
                : "Sony Headphones Controller";
        }

        #endregion
    }
}
