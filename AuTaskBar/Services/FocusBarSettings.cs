namespace AuTaskBar.Services
{
    public class FocusBarSettings
    {
        public bool Pinned { get; set; }
        public int PomodoroFocusMinutes { get; set; } = 25;
        public int PomodoroRestMinutes { get; set; } = 5;
        public string? LastMood { get; set; }
        public bool RunAtStartup { get; set; }
        public bool AnchorToTop { get; set; } = false;
        public double BarOpacity { get; set; } = 0.7;
        public double BarWidth { get; set; } = 1280;
        public double BarHeight { get; set; } = 90;
        public bool EnableMarquee { get; set; } = true;
        public string MenuPalette { get; set; } = "Dark";
        public string AppThemeMode { get; set; } = "Dark";
        public string AppTheme { get; set; } = "Ocean";
    }
}
