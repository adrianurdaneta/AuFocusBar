using System;
using System.Collections.Generic;
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
                        ? TimeSpan.FromMinutes(Settings.PomodoroFocusMinutes)
                        : TimeSpan.FromMinutes(Settings.PomodoroRestMinutes);
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
                _pomodoroState = _pomodoroState == PomodoroState.Focus ? PomodoroState.Rest : PomodoroState.Focus;
                _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus
                    ? TimeSpan.FromMinutes(Settings.PomodoroFocusMinutes)
                    : TimeSpan.FromMinutes(Settings.PomodoroRestMinutes);
            }
            else
            {
                _pomodoroRemainingTs = _pomodoroRemainingTs.Subtract(TimeSpan.FromSeconds(1));
            }
            UpdatePomodoroBindings();
        }

        public void UpdatePomodoroBindings()
        {
            var totalSeconds = _pomodoroState == PomodoroState.Focus
                ? Math.Max(1, Settings.PomodoroFocusMinutes * 60)
                : Math.Max(1, Settings.PomodoroRestMinutes * 60);

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
    }
}

