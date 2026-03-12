using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuTaskBar.Services;
using System.Globalization;

namespace AuTaskBar.ViewModels
{
    public partial class FocusBarViewModel : ObservableObject
    {
        private readonly ITaskService _taskService;
        private readonly ICalendarService _calendarService;
        
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _pomodoroTimer;

        // XAML Bound Properties
        [ObservableProperty] private bool _isClickThrough;
        [ObservableProperty] private bool _isAnchoredTop;
        [ObservableProperty] private string _pinMenuHeader = "Fijar barra (Topmost ON)";
        [ObservableProperty] private string _pomodoroMenuHeader = "Iniciar Pomodoro";
        
        [ObservableProperty] private Brush _menuBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x2D));
        [ObservableProperty] private Brush _menuHoverBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x3B, 0x4A));
        [ObservableProperty] private Brush _menuBorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0x56, 0x66));
        [ObservableProperty] private Brush _menuTextBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF0));
        [ObservableProperty] private Brush _menuAccentBrush = new SolidColorBrush(Color.FromRgb(0x58, 0x9D, 0xFF));
        
        [ObservableProperty] private string _taskText = string.Empty;
        [ObservableProperty] private string _taskEditText = string.Empty;
        [ObservableProperty] private bool _isTaskEditing;

        [ObservableProperty] private string _dateText = string.Empty;
        [ObservableProperty] private string _localTimeMadrid = string.Empty;
        [ObservableProperty] private string _timeVZLA = string.Empty;
        
        [ObservableProperty] private string _dayNightIconEsp = "\u2600";
        [ObservableProperty] private Brush _dayNightIconBrushEsp = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
        [ObservableProperty] private string _dayNightIconVen = "\u2600";
        [ObservableProperty] private Brush _dayNightIconBrushVen = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
        
        [ObservableProperty] private Brush _pomodoroBrush = Brushes.Green;
        [ObservableProperty] private string _pomodoroRemaining = "25:00";
        [ObservableProperty] private string _pomodoroProgressText = "100%";
        [ObservableProperty] private double _pomodoroProgressPercentage = 100;
        
        [ObservableProperty] private string _nextMeetingText = string.Empty;
        [ObservableProperty] private string _dailyEnglish = string.Empty;
        
        [ObservableProperty] private Brush _windowBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x11, 0xAA));
        [ObservableProperty] private Brush _glassBackgroundBrush = Brushes.Transparent;
        [ObservableProperty] private Brush _barBorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        [ObservableProperty] private Brush _primaryTextBrush = Brushes.White;
        [ObservableProperty] private Brush _secondaryTextBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        [ObservableProperty] private Brush _separatorBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        
        [ObservableProperty] private double _barSurfaceOpacity = 1;
        [ObservableProperty] private double _barShadowOpacity = 1;

        // Code-behind injected commands
        [ObservableProperty] private ICommand? _togglePinCommand;
        [ObservableProperty] private ICommand? _toggleAnchorTopCommand;
        [ObservableProperty] private ICommand? _toggleClickThroughCommand;
        [ObservableProperty] private ICommand? _resetPositionCommand;
        [ObservableProperty] private ICommand? _openSettingsCommand;
        [ObservableProperty] private ICommand? _exitAppCommand;

        // Custom logic events
        public event EventHandler? MarqueeRefreshRequested;

        // Timers state
        private TimeSpan _pomodoroRemainingTs = TimeSpan.Zero;
        private bool _pomodoroRunning = false;
        private PomodoroState _pomodoroState = PomodoroState.Focus;

        // We will keep a reference to current settings for Pomodoro
        public FocusBarSettings Settings { get; set; } = new FocusBarSettings();

        public FocusBarViewModel()
        {
            _taskService = new TaskService();
            _calendarService = new CalendarService();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;

            _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pomodoroTimer.Tick += PomodoroTimer_Tick;
        }

        public void Initialize()
        {
            TaskText = _taskService.GetPrimaryTask();
            TaskEditText = TaskText;
            DailyEnglish = PickRandomEnglishPill();

            UpdateClocks();
            UpdateNextMeeting();

            _clockTimer.Start();
        }

        partial void OnTaskTextChanged(string value)
        {
            if (!IsTaskEditing)
            {
                TaskEditText = value;
            }
            MarqueeRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnNextMeetingTextChanged(string value)
        {
            MarqueeRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void RefreshTask()
        {
            TaskText = _taskService.GetPrimaryTask();
        }

        [RelayCommand]
        private void RefreshMeeting()
        {
            UpdateNextMeeting();
        }

        [RelayCommand]
        private void NewPill()
        {
            DailyEnglish = PickRandomEnglishPill();
        }

        [RelayCommand]
        public void TogglePomodoro()
        {
            var focusMinutes = Settings.PomodoroFocusMinutes > 0 ? Settings.PomodoroFocusMinutes : 25;
            var restMinutes = Settings.PomodoroRestMinutes > 0 ? Settings.PomodoroRestMinutes : 5;

            if (_pomodoroRunning)
            {
                _pomodoroTimer.Stop();
                _pomodoroRunning = false;
            }
            else
            {
                if (_pomodoroRemainingTs == TimeSpan.Zero)
                {
                    _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus
                        ? TimeSpan.FromMinutes(focusMinutes)
                        : TimeSpan.FromMinutes(restMinutes);
                }
                _pomodoroTimer.Start();
                _pomodoroRunning = true;
            }
            UpdatePomodoroBindings();
        }

        public void StartTimers()
        {
            if (!_clockTimer.IsEnabled) _clockTimer.Start();
            if (_pomodoroRunning && !_pomodoroTimer.IsEnabled) _pomodoroTimer.Start();
        }

        public void StopTimers()
        {
            _clockTimer.Stop();
            _pomodoroTimer.Stop();
        }

        public void RecomputePomodoroInitialState()
        {
            if (!_pomodoroRunning)
            {
                var focusMinutes = Settings.PomodoroFocusMinutes > 0 ? Settings.PomodoroFocusMinutes : 25;
                _pomodoroState = PomodoroState.Focus;
                _pomodoroRemainingTs = TimeSpan.Zero;
                PomodoroRemaining = TimeSpan.FromMinutes(focusMinutes).ToString(@"mm\:ss");
                PomodoroProgressPercentage = 100;
                PomodoroProgressText = "100%";
            }
        }

        public FocusBarSettings NormalizeSettings(FocusBarSettings settings)
        {
            if (settings.PomodoroFocusMinutes <= 0) settings.PomodoroFocusMinutes = 25;
            if (settings.PomodoroRestMinutes <= 0) settings.PomodoroRestMinutes = 5;
            if (settings.BarWidth < 700) settings.BarWidth = 1280;
            if (settings.BarHeight < 40) settings.BarHeight = 90;
            settings.BarOpacity = Math.Clamp(Math.Round(settings.BarOpacity, 2), 0.2, 1.0);
            if (string.IsNullOrWhiteSpace(settings.AppThemeMode)) settings.AppThemeMode = "Dark";
            if (string.IsNullOrWhiteSpace(settings.AppTheme)) settings.AppTheme = "Ocean";

            settings.BarWidth = Math.Clamp(settings.BarWidth, 700, 5000);
            settings.BarHeight = Math.Clamp(settings.BarHeight, 40, 400);

            Settings = settings;
            return settings;
        }

        public void ApplyVisualTheme(string theme, string themeMode, float surfaceAlphaBoost)
        {
            var p = GetAppThemePalette(theme);
            var isLight = IsLightThemeMode(themeMode);

            var windowBack = BoostAlpha(AdjustForTheme(p.WindowBack, isLight ? 1.15f : 0.85f), surfaceAlphaBoost);
            var glassTop = BoostAlpha(AdjustForTheme(p.GlassTop, isLight ? 1.1f : 0.9f), surfaceAlphaBoost);
            var glassMid = BoostAlpha(AdjustForTheme(p.GlassMid, isLight ? 1.1f : 0.9f), surfaceAlphaBoost);
            var glassBottom = BoostAlpha(AdjustForTheme(p.GlassBottom, isLight ? 1.1f : 0.9f), surfaceAlphaBoost);
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

        public void UpdateMenuState(bool isTopmost, string appTheme, string appThemeMode)
        {
            PinMenuHeader = isTopmost ? "Desfijar barra (Topmost OFF)" : "Fijar barra (Topmost ON)";

            var accent = GetAccentColor(appTheme);
            var theme = GetWpfMenuTheme(appThemeMode);

            MenuAccentBrush = new SolidColorBrush(accent);
            MenuBackgroundBrush = new SolidColorBrush(theme.Back);
            MenuHoverBrush = new SolidColorBrush(theme.Hover);
            MenuBorderBrush = new SolidColorBrush(theme.Border);
            MenuTextBrush = new SolidColorBrush(theme.Text);
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClocks();
        }

        private void UpdateClocks()
        {
            var now = DateTime.UtcNow;
            try
            {
                var tzMadrid = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
                var tzVenezuela = TimeZoneInfo.FindSystemTimeZoneById("Venezuela Standard Time");
                var madrid = TimeZoneInfo.ConvertTimeFromUtc(now, tzMadrid);
                var vzla = TimeZoneInfo.ConvertTimeFromUtc(now, tzVenezuela);
                DateText = madrid.ToString("ddd dd/MM/yyyy", new CultureInfo("es-ES"));
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
                var madrid = DateTime.Now;
                var vzla = DateTime.Now.AddHours(-5);
                DateText = madrid.ToString("ddd dd/MM/yyyy", new CultureInfo("es-ES"));
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
            if (hour >= 6 && hour < 18)
            {
                var sunHeight = 1.0 - Math.Abs(12.0 - hour) / 6.0;
                sunHeight = Math.Clamp(sunHeight, 0.0, 1.0);
                byte r = 255;
                byte g = (byte)(196 + (45 * sunHeight));
                byte b = (byte)(64 + (55 * sunHeight));
                var color = Color.FromRgb(r, g, b);

                if (hour < 8 || hour >= 16) return ("\u25D4", color);
                if (hour < 11 || hour >= 14) return ("\u25D1", color);
                return ("\u2600", color);
            }

            var moon = GetMoonPhaseFraction(madridTime.Date);
            var nightColor = Color.FromRgb(0x74, 0xA9, 0xFF);
            if (moon < 0.03 || moon >= 0.97) return ("\u25CF", nightColor);
            if (moon < 0.22) return ("\u25D4", nightColor);
            if (moon < 0.28) return ("\u25D1", nightColor);
            if (moon < 0.47) return ("\u25D5", nightColor);
            if (moon < 0.53) return ("\u25CB", nightColor);
            if (moon < 0.72) return ("\u25D5", nightColor);
            if (moon < 0.78) return ("\u25D1", nightColor);
            return ("\u25D4", nightColor);
        }

        private static double GetMoonPhaseFraction(DateTime date)
        {
            var knownNewMoon = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
            var current = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            const double synodicMonth = 29.530588853;
            var days = (current - knownNewMoon).TotalDays;
            var phase = (days % synodicMonth) / synodicMonth;
            if (phase < 0) phase += 1;
            return phase;
        }

        private void PomodoroTimer_Tick(object? sender, EventArgs e)
        {
            if (_pomodoroRemainingTs.TotalSeconds <= 0)
            {
                var focusMinutes = Settings.PomodoroFocusMinutes > 0 ? Settings.PomodoroFocusMinutes : 25;
                var restMinutes = Settings.PomodoroRestMinutes > 0 ? Settings.PomodoroRestMinutes : 5;

                _pomodoroState = _pomodoroState == PomodoroState.Focus ? PomodoroState.Rest : PomodoroState.Focus;
                _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus
                    ? TimeSpan.FromMinutes(focusMinutes)
                    : TimeSpan.FromMinutes(restMinutes);
            }
            else
            {
                _pomodoroRemainingTs = _pomodoroRemainingTs.Subtract(TimeSpan.FromSeconds(1));
            }
            UpdatePomodoroBindings();
        }

        public void UpdatePomodoroBindings()
        {
            var focusMinutes = Settings.PomodoroFocusMinutes > 0 ? Settings.PomodoroFocusMinutes : 25;
            var restMinutes = Settings.PomodoroRestMinutes > 0 ? Settings.PomodoroRestMinutes : 5;

            var totalSeconds = _pomodoroState == PomodoroState.Focus
                ? Math.Max(1, focusMinutes * 60)
                : Math.Max(1, restMinutes * 60);

            var effectiveRemaining = _pomodoroRemainingTs == TimeSpan.Zero
                ? TimeSpan.FromSeconds(totalSeconds)
                : _pomodoroRemainingTs;

            var remainingSeconds = Math.Clamp(effectiveRemaining.TotalSeconds, 0, totalSeconds);

            var percentage = (remainingSeconds / totalSeconds) * 100d;

            PomodoroRemaining = effectiveRemaining.ToString(@"mm\:ss");
            PomodoroBrush = _pomodoroState == PomodoroState.Focus ? Brushes.LimeGreen : Brushes.DodgerBlue;
            PomodoroProgressPercentage = percentage;
            PomodoroProgressText = $"{Math.Round(percentage)}%";
            
            PomodoroMenuHeader = _pomodoroRunning ? "Detener Pomodoro" : "Iniciar Pomodoro";
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

        private enum PomodoroState { Focus, Rest }

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

        public static Color GetAccentColor(string primaryColor)
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

        public static bool IsLightThemeMode(string themeMode)
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
    }
}

