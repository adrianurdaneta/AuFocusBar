using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingIcon = System.Drawing.Icon;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSolidBrush = System.Drawing.SolidBrush;
using DrawingTextRenderingHint = System.Drawing.Text.TextRenderingHint;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using Forms = System.Windows.Forms;
using AuTaskBar.Services;
using Wpf.Ui.Appearance;

namespace AuTaskBar
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly TaskService _taskService = new TaskService();
        private readonly CalendarService _calendarService = new CalendarService();
        private readonly ISettingsService _settingsService = new SettingsService();
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _pomodoroTimer;
        private readonly Forms.NotifyIcon _trayIcon;
        private SettingsWindow? _settingsWindow;
        private FocusBarSettings _settings = new FocusBarSettings();

        private bool _clickThrough = false;
        private string _pinMenuHeader = "Fijar barra (Topmost ON)";
        private string _pomodoroMenuHeader = "Iniciar Pomodoro";
        private Brush _menuBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x2D));
        private Brush _menuHoverBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x3B, 0x4A));
        private Brush _menuBorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0x56, 0x66));
        private Brush _menuTextBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF0));
        private Brush _menuAccentBrush = new SolidColorBrush(Color.FromRgb(0x58, 0x9D, 0xFF));
        private string _taskText = "";
        private string _taskEditText = "";
        private bool _isTaskEditing;

        private string _dateText = "";
        private string _localTimeMadrid = "";
        private string _timeVZLA = "";
        private string _dayNightIconEsp = "☀";
        private Brush _dayNightIconBrushEsp = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
        private string _dayNightIconVen = "☀";
        private Brush _dayNightIconBrushVen = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
        private Brush _pomodoroBrush = Brushes.Green;
        private string _pomodoroRemaining = "25:00";
        private string _pomodoroProgressText = "100%";
        private double _pomodoroProgressPercentage = 100;
        private string _nextMeetingText = "";
        private string _dailyEnglish = "";
        private Brush _windowBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x11, 0xAA));
        private Brush _glassBackgroundBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF), 0),
                new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.4),
                new GradientStop(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF), 1)
            },
            new Point(0, 0),
            new Point(0, 1));
        private Brush _barBorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        private Brush _primaryTextBrush = Brushes.White;
        private Brush _secondaryTextBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        private Brush _separatorBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        private double _barSurfaceOpacity = 2;
        private double _barShadowOpacity = 2;

        private TimeSpan _pomodoroRemainingTs = TimeSpan.Zero;
        private bool _pomodoroRunning = false;
        private PomodoroState _pomodoroState = PomodoroState.Focus;

        private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupAppName = "AuTaskBarFocusBar";
        private const float SurfaceAlphaBoost = 1f;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string TaskText
        {
            get => _taskText;
            set => SetField(ref _taskText, value, nameof(TaskText));
        }

        public string TaskEditText { get => _taskEditText; set => SetField(ref _taskEditText, value, nameof(TaskEditText)); }
        public bool IsTaskEditing { get => _isTaskEditing; set => SetField(ref _isTaskEditing, value, nameof(IsTaskEditing)); }

        public string DateText { get => _dateText; set => SetField(ref _dateText, value, nameof(DateText)); }
        public string LocalTimeMadrid { get => _localTimeMadrid; set => SetField(ref _localTimeMadrid, value, nameof(LocalTimeMadrid)); }
        public string TimeVZLA { get => _timeVZLA; set => SetField(ref _timeVZLA, value, nameof(TimeVZLA)); }
        public string DayNightIconEsp { get => _dayNightIconEsp; set => SetField(ref _dayNightIconEsp, value, nameof(DayNightIconEsp)); }
        public Brush DayNightIconBrushEsp { get => _dayNightIconBrushEsp; set => SetField(ref _dayNightIconBrushEsp, value, nameof(DayNightIconBrushEsp)); }
        public string DayNightIconVen { get => _dayNightIconVen; set => SetField(ref _dayNightIconVen, value, nameof(DayNightIconVen)); }
        public Brush DayNightIconBrushVen { get => _dayNightIconBrushVen; set => SetField(ref _dayNightIconBrushVen, value, nameof(DayNightIconBrushVen)); }
        public Brush PomodoroBrush { get => _pomodoroBrush; set => SetField(ref _pomodoroBrush, value, nameof(PomodoroBrush)); }
        public string PomodoroRemaining { get => _pomodoroRemaining; set => SetField(ref _pomodoroRemaining, value, nameof(PomodoroRemaining)); }
        public string PomodoroProgressText { get => _pomodoroProgressText; set => SetField(ref _pomodoroProgressText, value, nameof(PomodoroProgressText)); }
        public double PomodoroProgressPercentage { get => _pomodoroProgressPercentage; set => SetField(ref _pomodoroProgressPercentage, value, nameof(PomodoroProgressPercentage)); }
        public string NextMeetingText { get => _nextMeetingText; set => SetField(ref _nextMeetingText, value, nameof(NextMeetingText)); }
        public string DailyEnglish { get => _dailyEnglish; set => SetField(ref _dailyEnglish, value, nameof(DailyEnglish)); }
        public Brush WindowBackgroundBrush { get => _windowBackgroundBrush; set => SetField(ref _windowBackgroundBrush, value, nameof(WindowBackgroundBrush)); }
        public Brush GlassBackgroundBrush { get => _glassBackgroundBrush; set => SetField(ref _glassBackgroundBrush, value, nameof(GlassBackgroundBrush)); }
        public Brush BarBorderBrush { get => _barBorderBrush; set => SetField(ref _barBorderBrush, value, nameof(BarBorderBrush)); }
        public Brush PrimaryTextBrush { get => _primaryTextBrush; set => SetField(ref _primaryTextBrush, value, nameof(PrimaryTextBrush)); }
        public Brush SecondaryTextBrush { get => _secondaryTextBrush; set => SetField(ref _secondaryTextBrush, value, nameof(SecondaryTextBrush)); }
        public Brush SeparatorBrush { get => _separatorBrush; set => SetField(ref _separatorBrush, value, nameof(SeparatorBrush)); }
        public double BarSurfaceOpacity { get => _barSurfaceOpacity; set => SetField(ref _barSurfaceOpacity, value, nameof(BarSurfaceOpacity)); }
        public double BarShadowOpacity { get => _barShadowOpacity; set => SetField(ref _barShadowOpacity, value, nameof(BarShadowOpacity)); }
        public bool IsClickThrough { get => _clickThrough; set => SetField(ref _clickThrough, value, nameof(IsClickThrough)); }
        public string PinMenuHeader { get => _pinMenuHeader; set => SetField(ref _pinMenuHeader, value, nameof(PinMenuHeader)); }
        public string PomodoroMenuHeader { get => _pomodoroMenuHeader; set => SetField(ref _pomodoroMenuHeader, value, nameof(PomodoroMenuHeader)); }
        public Brush MenuBackgroundBrush { get => _menuBackgroundBrush; set => SetField(ref _menuBackgroundBrush, value, nameof(MenuBackgroundBrush)); }
        public Brush MenuHoverBrush { get => _menuHoverBrush; set => SetField(ref _menuHoverBrush, value, nameof(MenuHoverBrush)); }
        public Brush MenuBorderBrush { get => _menuBorderBrush; set => SetField(ref _menuBorderBrush, value, nameof(MenuBorderBrush)); }
        public Brush MenuTextBrush { get => _menuTextBrush; set => SetField(ref _menuTextBrush, value, nameof(MenuTextBrush)); }
        public Brush MenuAccentBrush { get => _menuAccentBrush; set => SetField(ref _menuAccentBrush, value, nameof(MenuAccentBrush)); }

        public ICommand TogglePinCommand { get; }
        public ICommand ToggleClickThroughCommand { get; }
        public ICommand TogglePomodoroCommand { get; }
        public ICommand RefreshTaskCommand { get; }
        public ICommand RefreshMeetingCommand { get; }
        public ICommand NewPillCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ExitAppCommand { get; }

        public MainWindow()
        {
            TogglePinCommand = new DelegateCommand(_ => TogglePinned());
            ToggleClickThroughCommand = new DelegateCommand(_ => ToggleClickThrough());
            TogglePomodoroCommand = new DelegateCommand(_ => PomodoroButton_Click(this, new RoutedEventArgs()));
            RefreshTaskCommand = new DelegateCommand(_ => TaskText = _taskService.GetPrimaryTask());
            RefreshMeetingCommand = new DelegateCommand(_ => UpdateNextMeeting());
            NewPillCommand = new DelegateCommand(_ => DailyEnglish = PickRandomEnglishPill());
            OpenSettingsCommand = new DelegateCommand(_ => OpenSettings());
            ExitAppCommand = new DelegateCommand(_ => Close());

            InitializeComponent();
            DataContext = this;

            Loaded += (_, __) => Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Loaded);
            SizeChanged += (_, __) => Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Background);

            // Initial task load (mocked). In future replace with async fetch from TickTick API.
            TaskText = _taskService.GetPrimaryTask();
            TaskEditText = TaskText;

            // Setup clocks
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            // Setup pomodoro timer (1s ticks)
            _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pomodoroTimer.Tick += PomodoroTimer_Tick;

            // Initial values
            UpdateClocks();
            DailyEnglish = PickRandomEnglishPill();
            UpdateNextMeeting();

            _settings = _settingsService.Load();

            // Start as not click-through (window will receive clicks)
            IsClickThrough = false;
            SetClickThrough(false);

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = ResolveTrayIcon(),
                Visible = true,
                Text = "FocusBar"
            };

            _trayIcon.DoubleClick += (_, __) => Dispatcher.Invoke(() =>
            {
                Topmost = !Topmost;
                IsClickThrough = false;
                SetClickThrough(false);
                _settings.Pinned = Topmost;
                _settingsService.Save(_settings);
                UpdateContextMenuState();
            });

            _trayIcon.MouseUp += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Right)
                {
                    Dispatcher.Invoke(ShowContextMenu);
                }
            };

            ApplySettings();

            Closed += (_, __) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            };
        }

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

        private void ApplyAppTheme(string theme, string themeMode)
        {
            var p = GetAppThemePalette(theme);
            var isLight = IsLightThemeMode(themeMode);

            var windowBack = BoostAlpha(AdjustForTheme(p.WindowBack, isLight ? 1.15f : 0.85f), SurfaceAlphaBoost);
            var glassTop = BoostAlpha(AdjustForTheme(p.GlassTop, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var glassMid = BoostAlpha(AdjustForTheme(p.GlassMid, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var glassBottom = BoostAlpha(AdjustForTheme(p.GlassBottom, isLight ? 1.1f : 0.9f), SurfaceAlphaBoost);
            var border = AdjustForTheme(p.Border, isLight ? 1.12f : 0.88f);

            WindowBackgroundBrush = new SolidColorBrush(windowBack);
            GlassBackgroundBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(glassTop, 0),
                    new GradientStop(glassMid, 0.4),
                    new GradientStop(glassBottom, 1)
                },
                new Point(0, 0),
                new Point(0, 1));
            BarBorderBrush = new SolidColorBrush(border);
            PrimaryTextBrush = new SolidColorBrush(p.Primary);
            SecondaryTextBrush = new SolidColorBrush(p.Secondary);
            SeparatorBrush = new SolidColorBrush(p.Separator);
        }

        private static bool IsLightThemeMode(string themeMode)
        {
            if (string.Equals(themeMode, "Light", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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

        private static DrawingIcon ResolveTrayIcon()
        {
            var customPath = Path.Combine(AppContext.BaseDirectory, "Assets", "focusbar.ico");
            if (File.Exists(customPath))
            {
                return new DrawingIcon(customPath);
            }

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var exeIcon = DrawingIcon.ExtractAssociatedIcon(exePath);
                if (exeIcon != null)
                {
                    return exeIcon;
                }
            }

            return DrawingSystemIcons.Application;
        }

        // Allow dragging the window by mouse down anywhere on the grid
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (ReferenceEquals(source, TaskTextMarquee) || ReferenceEquals(source, TaskEditBox))
                {
                    return;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            try { DragMove(); } catch { }
        }

        private void TaskTextMarquee_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            IsTaskEditing = true;
            TaskEditText = TaskText;

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
                if (!string.IsNullOrWhiteSpace(TaskEditText))
                {
                    TaskText = TaskEditText.Trim();
                }

                IsTaskEditing = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TaskEditText = TaskText;
                IsTaskEditing = false;
                e.Handled = true;
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        }

        private static bool IsInteractiveElement(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is ButtonBase || obj is TextBoxBase || obj is ComboBox || obj is Slider)
                {
                    return true;
                }

                if (obj is TextBlock tb && tb.Tag is string)
                {
                    return true;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }

            return false;
        }

        private void PinToggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePinned();
        }

        private void ShowContextMenu()
        {
            UpdateContextMenuState();

            if (BarContextMenu == null)
            {
                return;
            }

            BarContextMenu.DataContext = this;
            BarContextMenu.Placement = PlacementMode.MousePoint;
            BarContextMenu.IsOpen = true;
        }

        private void TogglePinned()
        {
            Topmost = !Topmost;
            IsClickThrough = false;
            SetClickThrough(false);
            _settings.Pinned = Topmost;
            _settingsService.Save(_settings);
            UpdateContextMenuState();
        }

        private void ToggleClickThrough()
        {
            IsClickThrough = !IsClickThrough;
            SetClickThrough(IsClickThrough);
            UpdateContextMenuState();
        }

        private void UpdateContextMenuState()
        {
            PinMenuHeader = Topmost ? "Desfijar barra (Topmost OFF)" : "Fijar barra (Topmost ON)";
            PomodoroMenuHeader = _pomodoroRunning ? "Detener Pomodoro" : "Iniciar Pomodoro";

            var accent = GetAccentColor(_settings.AppTheme);
            var theme = GetWpfMenuTheme(_settings.AppThemeMode);

            MenuAccentBrush = new SolidColorBrush(accent);
            MenuBackgroundBrush = new SolidColorBrush(theme.Back);
            MenuHoverBrush = new SolidColorBrush(theme.Hover);
            MenuBorderBrush = new SolidColorBrush(theme.Border);
            MenuTextBrush = new SolidColorBrush(theme.Text);
        }

        private static (Color Back, Color Hover, Color Border, Color Text) GetWpfMenuTheme(string mode)
        {
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
            {
                return (Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xE6, 0xEC, 0xF5), Color.FromRgb(0xCE, 0xD5, 0xDF), Color.FromRgb(0x20, 0x20, 0x20));
            }

            if (string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
            {
                var isDark = SystemParameters.WindowGlassColor.R + SystemParameters.WindowGlassColor.G + SystemParameters.WindowGlassColor.B < 382;
                return isDark
                    ? (Color.FromRgb(0x26, 0x26, 0x26), Color.FromRgb(0x35, 0x35, 0x35), Color.FromRgb(0x4A, 0x4A, 0x4A), Color.FromRgb(0xF2, 0xF2, 0xF2))
                    : (Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xE6, 0xEC, 0xF5), Color.FromRgb(0xCE, 0xD5, 0xDF), Color.FromRgb(0x20, 0x20, 0x20));
            }

            return (Color.FromRgb(0x1E, 0x24, 0x2D), Color.FromRgb(0x2F, 0x3B, 0x4A), Color.FromRgb(0x4C, 0x56, 0x66), Color.FromRgb(0xE6, 0xEA, 0xF0));
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

        private void OpenSettings()
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings)
            {
                Owner = this
            };

            _settingsWindow.SettingsSaved += settings =>
            {
                _settings = settings;
                _settingsService.Save(_settings);
                ApplySettings();
            };

            _settingsWindow.Closed += (_, __) => _settingsWindow = null;
            _settingsWindow.Show();
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
            ApplyAppTheme(_settings.AppTheme, _settings.AppThemeMode);
            ApplyWpfUiAccent(_settings.AppTheme, _settings.AppThemeMode);
            var barSurfaceOpacity = Math.Clamp(_settings.BarOpacity, 0.2, 1.0);
            var barShadowOpacity = Math.Clamp(barSurfaceOpacity * 0.6, 0.08, 0.6);

            BarSurfaceOpacity = barSurfaceOpacity;
            BarShadowOpacity = barShadowOpacity;
            Dispatcher.BeginInvoke(() => BarSurfaceOpacity = barSurfaceOpacity, DispatcherPriority.Render);
            Dispatcher.BeginInvoke(() => BarShadowOpacity = barShadowOpacity, DispatcherPriority.Render);
            UpdateContextMenuState();

            if (!_pomodoroRunning && _pomodoroRemainingTs == TimeSpan.Zero)
            {
                PomodoroRemaining = TimeSpan.FromMinutes(_settings.PomodoroFocusMinutes).ToString(@"mm\:ss");
            }

            SetRunAtStartup(_settings.RunAtStartup);

            RefreshMarqueeAnimations();
        }

        private static void SetRunAtStartup(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(StartupRunKey);

                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        key.SetValue(StartupAppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(StartupAppName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // Ignore registry errors to keep app non-blocking.
            }
        }

        private void UpdateNextMeeting()
        {
            var next = _calendarService.GetNextMeeting();
            if (next == null)
            {
                NextMeetingText = "Next: (no meetings)";
                return;
            }

            var span = next.Value.Start - DateTime.Now;
            var mins = (int)Math.Max(0, Math.Round(span.TotalMinutes));
            NextMeetingText = $"Next: {next.Value.Title} in {mins} min";
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClocks();
        }

        private void UpdateClocks()
        {
            var now = DateTime.UtcNow;
            // Madrid: CET/CEST (Europe/Madrid) - use TimeZoneInfo
            try
            {
                var tzMadrid = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
                var tzVenezuela = TimeZoneInfo.FindSystemTimeZoneById("Venezuela Standard Time");
                var madrid = TimeZoneInfo.ConvertTimeFromUtc(now, tzMadrid);
                var vzla = TimeZoneInfo.ConvertTimeFromUtc(now, tzVenezuela);
                DateText = madrid.ToString("dd/MM/yyyy");
                LocalTimeMadrid = madrid.ToString("hh:mm tt");
                TimeVZLA = vzla.ToString("hh:mm tt");

                var madridByHour = new DateTime(madrid.Year, madrid.Month, madrid.Day, madrid.Hour, 0, 0);
                var vzlaByHour = new DateTime(vzla.Year, vzla.Month, vzla.Day, vzla.Hour, 0, 0);
                var espAppearance = GetDayNightAppearance(madridByHour);
                var venAppearance = GetDayNightAppearance(vzlaByHour);

                DayNightIconEsp = espAppearance.Icon;
                DayNightIconBrushEsp = new SolidColorBrush(espAppearance.Color);
                DayNightIconVen = venAppearance.Icon;
                DayNightIconBrushVen = new SolidColorBrush(venAppearance.Color);
            }
            catch
            {
                // Fallback if timezone ids differ on OS
                var madrid = DateTime.Now;
                var vzla = DateTime.Now.AddHours(-5);
                DateText = madrid.ToString("dd/MM/yyyy");
                LocalTimeMadrid = madrid.ToString("hh:mm tt");
                TimeVZLA = vzla.ToString("hh:mm tt");

                var madridByHour = new DateTime(madrid.Year, madrid.Month, madrid.Day, madrid.Hour, 0, 0);
                var vzlaByHour = new DateTime(vzla.Year, vzla.Month, vzla.Day, vzla.Hour, 0, 0);
                var espAppearance = GetDayNightAppearance(madridByHour);
                var venAppearance = GetDayNightAppearance(vzlaByHour);

                DayNightIconEsp = espAppearance.Icon;
                DayNightIconBrushEsp = new SolidColorBrush(espAppearance.Color);
                DayNightIconVen = venAppearance.Icon;
                DayNightIconBrushVen = new SolidColorBrush(venAppearance.Color);
            }
        }

        private static (string Icon, Color Color) GetDayNightAppearance(DateTime madridTime)
        {
            var hour = madridTime.Hour + (madridTime.Minute / 60.0);

            // Daytime icon by rough sun height
            if (hour >= 6 && hour < 18)
            {
                // 0 at sunrise/sunset, 1 at midday
                var sunHeight = 1.0 - Math.Abs(12.0 - hour) / 6.0;
                sunHeight = Math.Clamp(sunHeight, 0.0, 1.0);

                // Yellow tone varies with sun height
                byte r = 255;
                byte g = (byte)(196 + (45 * sunHeight)); // 196..241
                byte b = (byte)(64 + (55 * sunHeight));  // 64..119
                var color = Color.FromRgb(r, g, b);

                if (hour < 8 || hour >= 16) return ("◔", color);
                if (hour < 11 || hour >= 14) return ("◑", color);
                return ("☀", color);
            }

            // Night icon by moon phase (blue shades)
            var moon = GetMoonPhaseFraction(madridTime.Date);
            var nightColor = Color.FromRgb(0x74, 0xA9, 0xFF);
            if (moon < 0.03 || moon >= 0.97) return ("●", nightColor);
            if (moon < 0.22) return ("◔", nightColor);
            if (moon < 0.28) return ("◑", nightColor);
            if (moon < 0.47) return ("◕", nightColor);
            if (moon < 0.53) return ("○", nightColor);
            if (moon < 0.72) return ("◕", nightColor);
            if (moon < 0.78) return ("◑", nightColor);
            return ("◔", nightColor);
        }

        private static double GetMoonPhaseFraction(DateTime date)
        {
            // Approximation based on a known new moon reference.
            var knownNewMoon = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
            var current = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            const double synodicMonth = 29.530588853;
            var days = (current - knownNewMoon).TotalDays;
            var phase = (days % synodicMonth) / synodicMonth;
            if (phase < 0) phase += 1;
            return phase;
        }

        private enum PomodoroState { Focus, Rest }

        private void PomodoroButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pomodoroRunning)
            {
                // stop
                _pomodoroTimer.Stop();
                _pomodoroRunning = false;
            }
            else
            {
                // start or resume
                if (_pomodoroRemainingTs == TimeSpan.Zero)
                {
                    // set default based on state
                    _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus
                        ? TimeSpan.FromMinutes(_settings.PomodoroFocusMinutes)
                        : TimeSpan.FromMinutes(_settings.PomodoroRestMinutes);
                }
                _pomodoroTimer.Start();
                _pomodoroRunning = true;
            }
            UpdatePomodoroBindings();
        }

        private void PomodoroTimer_Tick(object? sender, EventArgs e)
        {
            if (_pomodoroRemainingTs.TotalSeconds <= 0)
            {
                // switch state
                _pomodoroState = _pomodoroState == PomodoroState.Focus ? PomodoroState.Rest : PomodoroState.Focus;
                _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus
                    ? TimeSpan.FromMinutes(_settings.PomodoroFocusMinutes)
                    : TimeSpan.FromMinutes(_settings.PomodoroRestMinutes);
                // continue running
            }
            else
            {
                _pomodoroRemainingTs = _pomodoroRemainingTs.Subtract(TimeSpan.FromSeconds(1));
            }
            UpdatePomodoroBindings();
        }

        private void UpdatePomodoroBindings()
        {
            var totalSeconds = _pomodoroState == PomodoroState.Focus
                ? Math.Max(1, _settings.PomodoroFocusMinutes * 60)
                : Math.Max(1, _settings.PomodoroRestMinutes * 60);

            var remainingSeconds = _pomodoroRemainingTs == TimeSpan.Zero
                ? totalSeconds
                : Math.Clamp(_pomodoroRemainingTs.TotalSeconds, 0, totalSeconds);

            var percentage = (remainingSeconds / totalSeconds) * 100d;

            PomodoroRemaining = _pomodoroRemainingTs.ToString(@"mm\:ss");
            PomodoroBrush = _pomodoroState == PomodoroState.Focus ? Brushes.LimeGreen : Brushes.DodgerBlue;
            PomodoroProgressPercentage = percentage;
            PomodoroProgressText = $"{Math.Round(percentage)}%";
            UpdateContextMenuState();
        }

        private void Mood_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is string mood)
            {
                SetMood(mood);
            }
        }

        private void SetMood(string mood)
        {
            TaskText = $"Mood logged: {mood} - {DateTime.Now:t}";
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
            if (textBlock == null || viewport == null)
            {
                return;
            }

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
            var animation = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(endX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + scrollSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(endX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3 + scrollSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.2 + scrollSeconds))));

            transform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private readonly Random _rand = new Random();
        private string PickRandomEnglishPill()
        {
            var list = new List<string>
            {
                "Refactor: reduce technical debt",
                "Throttling",
                "Immutable data",
                "Circuit breaker",
                "Idempotent operations",
                "Event-driven architecture",
                "Backpressure",
                "CQRS",
                "Observability",
                "Dependency injection"
            };
            return list[_rand.Next(list.Count)];
        }

        // Set or unset click-through by changing window exstyle
        private void SetClickThrough(bool enable)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enable)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
                // Toolwindow hides from alt-tab; we already hide from taskbar but this helps.
            }
            else
            {
                // remove transparent and toolwindow flags
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT & ~WS_EX_TOOLWINDOW);
            }
        }

        // P/Invoke for window styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }

    internal sealed class DelegateCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public DelegateCommand(Action<object?> execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }

    internal sealed class FocusBarColorTable : Forms.ProfessionalColorTable
    {
        private readonly DrawingColor _back;
        private readonly DrawingColor _backAlt;
        private readonly DrawingColor _border;

        public FocusBarColorTable(DrawingColor back, DrawingColor backAlt, DrawingColor border)
        {
            _back = back;
            _backAlt = backAlt;
            _border = border;
        }

        public override DrawingColor ToolStripDropDownBackground => _back;
        public override DrawingColor ImageMarginGradientBegin => _back;
        public override DrawingColor ImageMarginGradientMiddle => _back;
        public override DrawingColor ImageMarginGradientEnd => _back;
        public override DrawingColor MenuBorder => _border;
        public override DrawingColor MenuItemBorder => _border;
        public override DrawingColor MenuItemSelected => _backAlt;
        public override DrawingColor MenuItemSelectedGradientBegin => _backAlt;
        public override DrawingColor MenuItemSelectedGradientEnd => _backAlt;
    }

    // Mocked service to return a primary task. Prepared for future TickTick API integration.
    public class TaskService
    {
        public string GetPrimaryTask()
        {
            // In future this method will be async and fetch from TickTick using user credentials / API.
            // For now return a static example task.
            return "Tarea principal: Revisar informe de progreso (25 minutos)";
        }
    }

    // Simple CalendarService mock
    public class CalendarService
    {
        public (string Title, DateTime Start)? GetNextMeeting()
        {
            // In future implement real calendar integration (Graph API / Google Calendar).
            // For now return a meeting 12 minutes from now.
            var start = DateTime.Now.AddMinutes(12);
            return ("Sprint Sync", start);
        }
    }

    // Helper for property set
    public partial class MainWindow
    {
        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

                if (propertyName == nameof(TaskText) || propertyName == nameof(NextMeetingText))
                {
                    if (propertyName == nameof(TaskText) && !IsTaskEditing)
                    {
                        TaskEditText = TaskText;
                    }

                    Dispatcher.BeginInvoke(RefreshMarqueeAnimations, DispatcherPriority.Background);
                }
            }
        }
    }
}
