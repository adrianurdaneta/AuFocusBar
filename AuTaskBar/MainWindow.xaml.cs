using System;
using System.ComponentModel;
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
using Microsoft.Win32;
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
        private readonly Forms.NotifyIcon _trayIcon;
        private SettingsWindow? _settingsWindow;
        private FocusBarSettings _settings = new FocusBarSettings();

        public FocusBarViewModel ViewModel { get; }

        private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupAppName = "AuTaskBarFocusBar";
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
            ViewModel.PinMenuHeader = Topmost ? "Desfijar barra (Topmost OFF)" : "Fijar barra (Topmost ON)";
            var accent = GetAccentColor(_settings.AppTheme);
            var theme = GetWpfMenuTheme(_settings.AppThemeMode);

            ViewModel.MenuAccentBrush = new SolidColorBrush(accent);
            ViewModel.MenuBackgroundBrush = new SolidColorBrush(theme.Back);
            ViewModel.MenuHoverBrush = new SolidColorBrush(theme.Hover);
            ViewModel.MenuBorderBrush = new SolidColorBrush(theme.Border);
            ViewModel.MenuTextBrush = new SolidColorBrush(theme.Text);
        }

        private void ApplySettings()
        {
            if (_settings.PomodoroFocusMinutes <= 0) _settings.PomodoroFocusMinutes = 25;
            if (_settings.PomodoroRestMinutes <= 0) _settings.PomodoroRestMinutes = 5;
            if (_settings.BarWidth < 700) _settings.BarWidth = 1280;
            if (_settings.BarHeight < 40) _settings.BarHeight = 90;
            _settings.BarOpacity = Math.Clamp(Math.Round(_settings.BarOpacity, 2), 0.2, 1.0);
            if (string.IsNullOrWhiteSpace(_settings.AppThemeMode)) _settings.AppThemeMode = "Dark";
            if (string.IsNullOrWhiteSpace(_settings.AppTheme)) _settings.AppTheme = "Ocean";

            var barWidth = Math.Clamp(_settings.BarWidth, 700, 5000);
            var barHeight = Math.Clamp(_settings.BarHeight, 40, 400);
            _settings.BarWidth = barWidth;
            _settings.BarHeight = barHeight;

            Topmost = _settings.Pinned;
            Width = barWidth;
            Height = barHeight;

            ViewModel.IsAnchoredTop = _settings.AnchorToTop;
            if (_settings.AnchorToTop)
            {
                SnapToTopCenter();
            }
            
            ApplyAppTheme(_settings.AppTheme, _settings.AppThemeMode);
            ApplyWpfUiAccent(_settings.AppTheme, _settings.AppThemeMode);
            
            var barSurfaceOpacity = Math.Clamp(_settings.BarOpacity, 0.2, 1.0);
            var barShadowOpacity = Math.Clamp(barSurfaceOpacity * 0.6, 0.08, 0.6);

            ViewModel.BarSurfaceOpacity = barSurfaceOpacity;
            ViewModel.BarShadowOpacity = barShadowOpacity;
            
            UpdateContextMenuState();
            ViewModel.RecomputePomodoroInitialState();

            SetRunAtStartup(_settings.RunAtStartup);
            RefreshMarqueeAnimations();
        }

        private void ApplyAppTheme(string theme, string themeMode)
        {
            var p = GetAppThemePalette(theme);
            var isLight = IsLightThemeMode(themeMode);

            var windowBack = BoostAlpha(AdjustForTheme(p.WindowBack, isLight ? 1.15f : 0.85f), SurfaceAlphaBoost);
            var glassTop = BoostAlpha(AdjustForTheme(p.GlassTop, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var glassMid = BoostAlpha(AdjustForTheme(p.GlassMid, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var glassBottom = BoostAlpha(AdjustForTheme(p.GlassBottom, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var border = AdjustForTheme(p.Border, isLight ? 1.12f : 0.88f);

            ViewModel.WindowBackgroundBrush = new SolidColorBrush(windowBack);
            ViewModel.GlassBackgroundBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(glassTop, 0),
                    new GradientStop(glassMid, 0.4),
                    new GradientStop(glassBottom, 1)
                },
                new Point(0, 0),
                new Point(0, 1));
            ViewModel.BarBorderBrush = new SolidColorBrush(border);
            ViewModel.PrimaryTextBrush = new SolidColorBrush(p.Primary);
            ViewModel.SecondaryTextBrush = new SolidColorBrush(p.Secondary);
            ViewModel.SeparatorBrush = new SolidColorBrush(p.Separator);
        }

        // --- Theme / UI Helpers ---
        private static (Color WindowBack, Color GlassTop, Color GlassMid, Color GlassBottom, Color Border, Color Primary, Color Secondary, Color Separator) GetAppThemePalette(string theme)
        {
            return theme switch
            {
                "System" => (Color.FromArgb(0x33, 0x12, 0x14, 0x18), Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF), Colors.White, Color.FromRgb(0xDD, 0xDD, 0xDD), Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                "Amethyst" => (Color.FromArgb(0x33, 0x1D, 0x13, 0x2F), Color.FromArgb(0x40, 0xD7, 0xC3, 0xFF), Color.FromArgb(0x24, 0xB4, 0x95, 0xFF), Color.FromArgb(0x12, 0x90, 0x71, 0xD0), Color.FromArgb(0x66, 0xCE, 0xB8, 0xFF), Colors.White, Color.FromRgb(0xE9, 0xE1, 0xF7), Color.FromArgb(0x55, 0xCF, 0xBB, 0xFF)),
                "Emerald" => (Color.FromArgb(0x33, 0x0E, 0x22, 0x1A), Color.FromArgb(0x3A, 0x9D, 0xF0, 0xC0), Color.FromArgb(0x20, 0x71, 0xD4, 0xA4), Color.FromArgb(0x10, 0x4D, 0xB8, 0x89), Color.FromArgb(0x66, 0x9B, 0xE5, 0xBD), Colors.White, Color.FromRgb(0xDB, 0xEF, 0xE6), Color.FromArgb(0x55, 0xA8, 0xE8, 0xC9)),
                "Sunset" => (Color.FromArgb(0x33, 0x2A, 0x14, 0x12), Color.FromArgb(0x40, 0xFF, 0xC8, 0x9A), Color.FromArgb(0x22, 0xFF, 0xA8, 0x79), Color.FromArgb(0x10, 0xE8, 0x84, 0x5F), Color.FromArgb(0x66, 0xFF, 0xCC, 0xA8), Colors.White, Color.FromRgb(0xF7, 0xE7, 0xDB), Color.FromArgb(0x55, 0xFF, 0xD3, 0xB5)),
                "Rose" => (Color.FromArgb(0x33, 0x2A, 0x12, 0x1E), Color.FromArgb(0x40, 0xFF, 0xC3, 0xDA), Color.FromArgb(0x22, 0xFF, 0xA3, 0xC5), Color.FromArgb(0x10, 0xE8, 0x86, 0xB1), Color.FromArgb(0x66, 0xFF, 0xC7, 0xDF), Colors.White, Color.FromRgb(0xF6, 0xE2, 0xEB), Color.FromArgb(0x55, 0xFF, 0xCF, 0xE5)),
                "Amber" => (Color.FromArgb(0x33, 0x2B, 0x1F, 0x10), Color.FromArgb(0x40, 0xFF, 0xD8, 0x8A), Color.FromArgb(0x22, 0xFF, 0xC1, 0x62), Color.FromArgb(0x10, 0xE8, 0xA0, 0x3F), Color.FromArgb(0x66, 0xFF, 0xD7, 0x9E), Colors.White, Color.FromRgb(0xF9, 0xEB, 0xD6), Color.FromArgb(0x55, 0xFF, 0xDB, 0xAD)),
                "Violet" => (Color.FromArgb(0x33, 0x1D, 0x14, 0x31), Color.FromArgb(0x40, 0xCE, 0xC7, 0xFF), Color.FromArgb(0x22, 0xAF, 0xA2, 0xFF), Color.FromArgb(0x10, 0x8B, 0x7B, 0xEA), Color.FromArgb(0x66, 0xC8, 0xBF, 0xFF), Colors.White, Color.FromRgb(0xEA, 0xE5, 0xF8), Color.FromArgb(0x55, 0xD0, 0xC9, 0xFF)),
                _ => (Color.FromArgb(0x33, 0x00, 0x22, 0x44), Color.FromArgb(0x40, 0x00, 0x11, 0x55), Color.FromArgb(0x30, 0x00, 0x33, 0x66), Color.FromArgb(0x15, 0x00, 0x55, 0x88), Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), Colors.White, Color.FromRgb(0xEE, 0xEE, 0xEE), Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            };
        }

        private static bool IsLightThemeMode(string themeMode)
        {
            if (string.Equals(themeMode, "Light", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(themeMode, "System", StringComparison.OrdinalIgnoreCase))
            {
                return SystemParameters.WindowGlassColor.R + SystemParameters.WindowGlassColor.G + SystemParameters.WindowGlassColor.B > 382;
            }
            return false;
        }

        private static Color AdjustForTheme(Color color, float factor)
        {
            byte Scale(byte c) => (byte)Math.Clamp((int)(c * factor), 0, 255);
            return Color.FromArgb(color.A, Scale(color.R), Scale(color.G), Scale(color.B));
        }

        private static Color BoostAlpha(Color color, float factor)
        {
            var alpha = (byte)Math.Clamp((int)(color.A * factor), 0, 255);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static void ApplyWpfUiAccent(string primaryColor, string themeMode)
        {
            var accent = GetAccentColor(primaryColor);
            var appTheme = IsLightThemeMode(themeMode) ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationAccentColorManager.Apply(accent, appTheme, false, false);
        }
        
        private static Color GetAccentColor(string primaryColor)
        {
            return primaryColor switch
            {
                "Amethyst" => Color.FromRgb(0xA6, 0x7D, 0xF7),
                "Emerald" => Color.FromRgb(0x4E, 0xC5, 0x92),
                "Sunset" => Color.FromRgb(0xFF, 0x9C, 0x6B),
                "Rose" => Color.FromRgb(0xE8, 0x77, 0xA8),
                "Amber" => Color.FromRgb(0xF2, 0xB5, 0x44),
                "Violet" => Color.FromRgb(0x8D, 0x7B, 0xF9),
                _ => Color.FromRgb(0x58, 0x9D, 0xFF),
            };
        }

        private static (Color Back, Color Hover, Color Border, Color Text) GetWpfMenuTheme(string mode)
        {
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
                return (Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xE6, 0xEC, 0xF5), Color.FromRgb(0xCE, 0xD5, 0xDF), Color.FromRgb(0x20, 0x20, 0x20));

            if (string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
            {
                var isDark = SystemParameters.WindowGlassColor.R + SystemParameters.WindowGlassColor.G + SystemParameters.WindowGlassColor.B < 382;
                return isDark
                    ? (Color.FromRgb(0x26, 0x26, 0x26), Color.FromRgb(0x35, 0x35, 0x35), Color.FromRgb(0x4A, 0x4A, 0x4A), Color.FromRgb(0xF2, 0xF2, 0xF2))
                    : (Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xE6, 0xEC, 0xF5), Color.FromRgb(0xCE, 0xD5, 0xDF), Color.FromRgb(0x20, 0x20, 0x20));
            }

            return (Color.FromRgb(0x1E, 0x24, 0x2D), Color.FromRgb(0x2F, 0x3B, 0x4A), Color.FromRgb(0x4C, 0x56, 0x66), Color.FromRgb(0xE6, 0xEA, 0xF0));
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

        private static void SetRunAtStartup(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(StartupRunKey);
                if (key == null) return;
                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exePath)) key.SetValue(StartupAppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(StartupAppName, throwOnMissingValue: false);
                }
            }
            catch { }
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
