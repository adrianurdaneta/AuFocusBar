using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuTaskBar.Services;
using AuTaskBar.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Appearance;

using DrawingColor = System.Drawing.Color;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using Forms = System.Windows.Forms;

namespace AuTaskBar
{
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService = new SettingsService();
        private readonly IStartupService _startupService = new StartupService();
        private readonly Forms.NotifyIcon _trayIcon;
        private SettingsWindow? _settingsWindow;
        private FocusBarSettings _settings = new FocusBarSettings();

        public FocusBarViewModel ViewModel { get; }

        private const float SurfaceAlphaBoost = 1f;

        public MainWindow()
        {
            ViewModel = new FocusBarViewModel();
            DataContext = ViewModel;

            ViewModel.MarqueeRefreshRequested += (_, __) => 
                Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Background);

            InitializeComponent();

            Loaded += (_, __) => 
            {
                Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Loaded);
                ViewModel.Initialize();
            };
            SizeChanged += (_, __) => Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Background);
            LocationChanged += (_, __) =>
            {
                if (_settings.AnchorToTop)
                {
                    SnapToTopCenter();
                }
            };

            _settings = _settingsService.Load();
            ViewModel.Settings = _settings;

            // Start as not click-through
            SetClickThrough(false);

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = ResolveTrayIcon(),
                Visible = true,
                Text = "FocusBar"
            };

            _trayIcon.DoubleClick += (_, __) => Dispatcher.Invoke(TogglePinnedCore);

            _trayIcon.MouseUp += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Right)
                {
                    Dispatcher.Invoke(ShowContextMenu);
                }
            };

            ApplySettings();
            ViewModel.StartTimers();

            Closed += (_, __) =>
            {
                ViewModel.StopTimers();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            };
            
            // Reattach commands that need Window-level context
            ViewModel.TogglePinCommand = new RelayCommand(TogglePinnedCore);
            ViewModel.ToggleAnchorTopCommand = new RelayCommand(ToggleAnchorTopCore);
            ViewModel.ToggleClickThroughCommand = new RelayCommand(ToggleClickThroughCore);
            ViewModel.OpenSettingsCommand = new RelayCommand(OpenSettingsCore);
            ViewModel.ExitAppCommand = new RelayCommand(Close);
        }

        private void TogglePinnedCore()
        {
            Topmost = !Topmost;
            ViewModel.IsClickThrough = false;
            SetClickThrough(false);
            _settings.Pinned = Topmost;
            _settingsService.Save(_settings);
            UpdateContextMenuState();
        }

        private void ToggleClickThroughCore()
        {
            ViewModel.IsClickThrough = !ViewModel.IsClickThrough;
            SetClickThrough(ViewModel.IsClickThrough);
            UpdateContextMenuState();
        }

        private void ToggleAnchorTopCore()
        {
            _settings.AnchorToTop = !_settings.AnchorToTop;
            ViewModel.IsAnchoredTop = _settings.AnchorToTop;

            if (_settings.AnchorToTop)
            {
                SnapToTopCenter();
            }

            _settingsService.Save(_settings);
        }

        private void OpenSettingsCore()
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings) { Owner = this };

            _settingsWindow.SettingsSaved += settings =>
            {
                settings.AnchorToTop = _settings.AnchorToTop;
                _settings = settings;
                ViewModel.Settings = settings;
                _settingsService.Save(_settings);
                ApplySettings();
                ViewModel.UpdatePomodoroBindings();
            };

            _settingsWindow.Closed += (_, __) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        private void ShowContextMenu()
        {
            UpdateContextMenuState();
            if (BarContextMenu == null) return;
            BarContextMenu.DataContext = ViewModel;
            BarContextMenu.Placement = PlacementMode.MousePoint;
            BarContextMenu.IsOpen = true;
        }

        private void UpdateContextMenuState()
        {
            ViewModel.UpdateMenuState(Topmost, _settings.AppTheme, _settings.AppThemeMode);
        }

        private void ApplySettings()
        {
            _settings = ViewModel.NormalizeSettings(_settings);

            var barWidth = _settings.BarWidth;
            var barHeight = _settings.BarHeight;

            Topmost = _settings.Pinned;
            Width = barWidth;
            Height = barHeight;

            ViewModel.IsAnchoredTop = _settings.AnchorToTop;
            if (_settings.AnchorToTop)
            {
                SnapToTopCenter();
            }
            
            ViewModel.ApplyVisualTheme(_settings.AppTheme, _settings.AppThemeMode, SurfaceAlphaBoost);
            ApplyWpfUiAccent(_settings.AppTheme, _settings.AppThemeMode);
            
            var barSurfaceOpacity = Math.Clamp(_settings.BarOpacity, 0.2, 1.0);
            var barShadowOpacity = Math.Clamp(barSurfaceOpacity * 0.6, 0.08, 0.6);

            ViewModel.BarSurfaceOpacity = barSurfaceOpacity;
            ViewModel.BarShadowOpacity = barShadowOpacity;
            
            UpdateContextMenuState();
            ViewModel.RecomputePomodoroInitialState();

            _startupService.SetRunAtStartup(_settings.RunAtStartup);
            RefreshMarqueeAnimations();
        }

        private static void ApplyWpfUiAccent(string primaryColor, string themeMode)
        {
            var accent = FocusBarViewModel.GetAccentColor(primaryColor);
            var appTheme = FocusBarViewModel.IsLightThemeMode(themeMode) ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationAccentColorManager.Apply(accent, appTheme, false, false);
        }

        private static DrawingIcon ResolveTrayIcon()
        {
            var customPath = Path.Combine(AppContext.BaseDirectory, "Assets", "focusbar.ico");
            if (File.Exists(customPath)) return new DrawingIcon(customPath);
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var exeIcon = DrawingIcon.ExtractAssociatedIcon(exePath);
                if (exeIcon != null) return exeIcon;
            }
            return DrawingSystemIcons.Application;
        }

        // --- View Handlers ---
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (ReferenceEquals(source, TaskTextMarquee) || ReferenceEquals(source, TaskEditBox)) return;
                source = VisualTreeHelper.GetParent(source);
            }
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }

            if (_settings.AnchorToTop)
            {
                SnapToTopCenter();
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (IsInteractiveElement(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); } catch { }

            if (_settings.AnchorToTop)
            {
                SnapToTopCenter();
            }
        }

        private void SnapToTopCenter()
        {
            var workArea = SystemParameters.WorkArea;
            Top = workArea.Top;
            Left = workArea.Left + ((workArea.Width - Width) / 2.0);
        }

        private void TaskTextMarquee_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.IsTaskEditing = true;
            ViewModel.TaskEditText = ViewModel.TaskText;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                TaskEditBox.Focus();
                TaskEditBox.SelectAll();
            }), DispatcherPriority.Input);

            e.Handled = true;
        }

        private void TaskEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrWhiteSpace(ViewModel.TaskEditText)) ViewModel.TaskText = ViewModel.TaskEditText.Trim();
                ViewModel.IsTaskEditing = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ViewModel.TaskEditText = ViewModel.TaskText;
                ViewModel.IsTaskEditing = false;
                e.Handled = true;
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
        }

        private static bool IsInteractiveElement(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is ButtonBase || obj is TextBoxBase || obj is ComboBox || obj is Slider) return true;
                if (obj is TextBlock tb && tb.Tag is string) return true;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private void RefreshMarqueeAnimations()
        {
            if (!_settings.EnableMarquee)
            {
                StopMarquee(TaskTextMarquee);
                StopMarquee(NextTextMarquee);
                return;
            }

            ConfigureMarquee(TaskTextMarquee, TaskViewport, 7.0);
            ConfigureMarquee(NextTextMarquee, NextViewport, 6.0);
        }

        private static void StopMarquee(TextBlock textBlock)
        {
            if (textBlock.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                textBlock.RenderTransform = transform;
            }
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        }

        private static void ConfigureMarquee(TextBlock textBlock, FrameworkElement viewport, double scrollSeconds)
        {
            if (textBlock == null || viewport == null) return;

            if (textBlock.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                textBlock.RenderTransform = transform;
            }

            transform.BeginAnimation(TranslateTransform.XProperty, null);

            if (viewport.ActualWidth <= 0)
            {
                transform.X = 0;
                return;
            }

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var overflow = textBlock.DesiredSize.Width - viewport.ActualWidth;

            if (overflow <= 2)
            {
                transform.X = 0;
                textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
                return;
            }

            textBlock.TextTrimming = TextTrimming.None;

            var endX = -(overflow + 18);
            var animation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(endX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + scrollSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(endX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3 + scrollSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.2 + scrollSeconds))));

            transform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        // --- Interop (Click Through) ---
        private void SetClickThrough(bool enable)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enable)
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT & ~WS_EX_TOOLWINDOW);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }

}
