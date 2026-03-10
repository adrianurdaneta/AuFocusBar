using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using AuTaskBar.Services;

namespace AuTaskBar.ViewModels
{
    public class FocusBarViewModel : INotifyPropertyChanged
    {
        private readonly ITaskService _taskService;
        private readonly ICalendarService _calendarService;
        private readonly ISettingsService _settingsService;

        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _pomodoroTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _taskText = "";
        private string _dateText = "";
        private string _localTimeMadrid = "";
        private string _timeVZLA = "";
        private Brush _pomodoroBrush = Brushes.LimeGreen;
        private string _pomodoroRemaining = "";
        private string _nextMeetingText = "";
        private string _dailyEnglish = "";

        private TimeSpan _pomodoroRemainingTs = TimeSpan.Zero;
        private bool _pomodoroRunning = false;
        private PomodoroState _pomodoroState = PomodoroState.Focus;

        public FocusBarViewModel(ITaskService taskService, ICalendarService calendarService, ISettingsService settingsService)
        {
            _taskService = taskService;
            _calendarService = calendarService;
            _settingsService = settingsService;

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClocks();
            _clockTimer.Start();

            _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pomodoroTimer.Tick += (s, e) => PomodoroTick();

            TaskText = _taskService.GetPrimaryTask();
            DailyEnglish = PickRandomEnglishPill();
            UpdateNextMeeting();
            UpdateClocks();
        }

        public string TaskText { get => _taskText; set => SetField(ref _taskText, value); }
        public string DateText { get => _dateText; set => SetField(ref _dateText, value); }
        public string LocalTimeMadrid { get => _localTimeMadrid; set => SetField(ref _localTimeMadrid, value); }
        public string TimeVZLA { get => _timeVZLA; set => SetField(ref _timeVZLA, value); }
        public Brush PomodoroBrush { get => _pomodoroBrush; set => SetField(ref _pomodoroBrush, value); }
        public string PomodoroRemaining { get => _pomodoroRemaining; set => SetField(ref _pomodoroRemaining, value); }
        public string NextMeetingText { get => _nextMeetingText; set => SetField(ref _nextMeetingText, value); }
        public string DailyEnglish { get => _dailyEnglish; set => SetField(ref _dailyEnglish, value); }

        public bool IsPinned
        {
            get => _settingsService.Load().Pinned;
            set
            {
                var s = _settingsService.Load();
                s.Pinned = value;
                _settingsService.Save(s);
                OnPropertyChanged(nameof(IsPinned));
            }
        }

        public int PomodoroFocusMinutes
        {
            get => _settingsService.Load().PomodoroFocusMinutes;
            set { var s = _settingsService.Load(); s.PomodoroFocusMinutes = value; _settingsService.Save(s); }
        }

        public int PomodoroRestMinutes
        {
            get => _settingsService.Load().PomodoroRestMinutes;
            set { var s = _settingsService.Load(); s.PomodoroRestMinutes = value; _settingsService.Save(s); }
        }

        public string Mood
        {
            get => _settingsService.Load().LastMood ?? "";
            set { var s = _settingsService.Load(); s.LastMood = value; _settingsService.Save(s); OnPropertyChanged(nameof(Mood)); }
        }

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
                    _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus ? TimeSpan.FromMinutes(PomodoroFocusMinutes) : TimeSpan.FromMinutes(PomodoroRestMinutes);
                }
                _pomodoroTimer.Start();
                _pomodoroRunning = true;
            }
            UpdatePomodoroBindings();
        }

        public void TogglePin()
        {
            IsPinned = !IsPinned;
            OnPropertyChanged(nameof(IsPinned));
        }

        public void RefreshTask() => TaskText = _taskService.GetPrimaryTask();
        public void UpdateNextMeeting()
        {
            var next = _calendarService.GetNextMeeting();
            if (next == null) { NextMeetingText = "Next: (no meetings)"; return; }
            var span = next.Value.Start - DateTime.Now;
            var mins = (int)Math.Max(0, Math.Round(span.TotalMinutes));
            NextMeetingText = $"Next: {next.Value.Title} in {mins} min";
        }

        private void PomodoroTick()
        {
            if (_pomodoroRemainingTs.TotalSeconds <= 0)
            {
                _pomodoroState = _pomodoroState == PomodoroState.Focus ? PomodoroState.Rest : PomodoroState.Focus;
                _pomodoroRemainingTs = _pomodoroState == PomodoroState.Focus ? TimeSpan.FromMinutes(PomodoroFocusMinutes) : TimeSpan.FromMinutes(PomodoroRestMinutes);
            }
            else _pomodoroRemainingTs = _pomodoroRemainingTs.Subtract(TimeSpan.FromSeconds(1));
            UpdatePomodoroBindings();
        }

        private void UpdatePomodoroBindings()
        {
            PomodoroRemaining = _pomodoroRemainingTs.ToString(@"mm\:ss");
            PomodoroBrush = _pomodoroState == PomodoroState.Focus ? Brushes.LimeGreen : Brushes.DodgerBlue;
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
                DateText = madrid.ToString("ddd dd MMM");
                LocalTimeMadrid = madrid.ToString("HH:mm");
                TimeVZLA = "VEN " + vzla.ToString("HH:mm");
            }
            catch
            {
                var madrid = DateTime.Now;
                DateText = madrid.ToString("ddd dd MMM");
                LocalTimeMadrid = madrid.ToString("HH:mm");
                TimeVZLA = DateTime.Now.AddHours(-5).ToString("HH:mm");
            }
        }

        private readonly Random _rand = new Random();
        private string PickRandomEnglishPill()
        {
            var list = new[] { "Refactor", "Throttling", "Immutable", "Circuit breaker", "Idempotent", "Event-driven", "Backpressure", "CQRS", "Observability", "DI" };
            return list[_rand.Next(list.Length)];
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged(name);
            }
        }

        protected void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private enum PomodoroState { Focus, Rest }
    }
}
