using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuTaskBar.Services;

namespace AuTaskBar
{
    public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
    {
        public FocusBarSettings ResultSettings { get; private set; }
        public event Action<FocusBarSettings>? SettingsSaved;

        public SettingsWindow(FocusBarSettings currentSettings)
        {
            InitializeComponent();

            ResultSettings = new FocusBarSettings
            {
                Pinned = currentSettings.Pinned,
                PomodoroFocusMinutes = currentSettings.PomodoroFocusMinutes,
                PomodoroRestMinutes = currentSettings.PomodoroRestMinutes,
                LastMood = currentSettings.LastMood,
                RunAtStartup = currentSettings.RunAtStartup,
                BarOpacity = currentSettings.BarOpacity,
                BarWidth = currentSettings.BarWidth,
                BarHeight = currentSettings.BarHeight,
                EnableMarquee = currentSettings.EnableMarquee,
                MenuPalette = currentSettings.MenuPalette,
                AppThemeMode = currentSettings.AppThemeMode,
                AppTheme = currentSettings.AppTheme
            };

            ApplySettingsVisualTheme(ResultSettings.AppThemeMode, ResultSettings.AppTheme);

            FocusMinutesNumberBox.Value = ResultSettings.PomodoroFocusMinutes;
            RestMinutesNumberBox.Value = ResultSettings.PomodoroRestMinutes;
            BarWidthNumberBox.Value = ResultSettings.BarWidth;
            BarHeightNumberBox.Value = ResultSettings.BarHeight;
            BarOpacityNumberBox.Value = ResultSettings.BarOpacity * 100d;
            MarqueeToggle.IsChecked = ResultSettings.EnableMarquee;
            SelectThemeMode(ResultSettings.AppThemeMode);
            SelectPrimaryColor(ResultSettings.AppTheme);
            PinnedToggle.IsChecked = ResultSettings.Pinned;
            StartupToggle.IsChecked = ResultSettings.RunAtStartup;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var focus = (int)Math.Round(FocusMinutesNumberBox.Value ?? 25d);
            if (focus < 1 || focus > 240)
            {
                MessageBox.Show("Focus debe estar entre 1 y 240 minutos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rest = (int)Math.Round(RestMinutesNumberBox.Value ?? 5d);
            if (rest < 1 || rest > 120)
            {
                MessageBox.Show("Descanso debe estar entre 1 y 120 minutos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var barWidth = BarWidthNumberBox.Value ?? 1280d;
            if (barWidth < 700 || barWidth > 5000)
            {
                MessageBox.Show("Ancho debe estar entre 700 y 5000 px.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var barHeight = BarHeightNumberBox.Value ?? 90d;
            if (barHeight < 40 || barHeight > 400)
            {
                MessageBox.Show("Alto debe estar entre 40 y 400 px.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var barOpacityPercent = BarOpacityNumberBox.Value ?? 70d;
            if (barOpacityPercent < 20 || barOpacityPercent > 100)
            {
                MessageBox.Show("Opacidad debe estar entre 20% y 100%.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var barOpacity = Math.Clamp(Math.Round(barOpacityPercent / 100d, 2), 0.2, 1.0);

            ResultSettings.PomodoroFocusMinutes = focus;
            ResultSettings.PomodoroRestMinutes = rest;
            ResultSettings.BarWidth = barWidth;
            ResultSettings.BarHeight = barHeight;
            ResultSettings.BarOpacity = barOpacity;
            ResultSettings.EnableMarquee = MarqueeToggle.IsChecked == true;
            ResultSettings.AppThemeMode = (AppThemeModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Dark";
            ResultSettings.AppTheme = (PrimaryColorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ocean";
            ResultSettings.Pinned = PinnedToggle.IsChecked == true;
            ResultSettings.RunAtStartup = StartupToggle.IsChecked == true;

            SettingsSaved?.Invoke(ResultSettings);
            Close();
        }

        private void SelectThemeMode(string mode)
        {
            foreach (var item in AppThemeModeComboBox.Items)
            {
                if (item is ComboBoxItem option && string.Equals(option.Content?.ToString(), mode, StringComparison.OrdinalIgnoreCase))
                {
                    AppThemeModeComboBox.SelectedItem = option;
                    return;
                }
            }

            AppThemeModeComboBox.SelectedIndex = 0;
        }

        private void SelectPrimaryColor(string color)
        {
            foreach (var item in PrimaryColorComboBox.Items)
            {
                if (item is ComboBoxItem option && string.Equals(option.Content?.ToString(), color, StringComparison.OrdinalIgnoreCase))
                {
                    PrimaryColorComboBox.SelectedItem = option;
                    return;
                }
            }

            PrimaryColorComboBox.SelectedIndex = 0;
        }

        private void ApplySettingsVisualTheme(string themeMode, string primaryColor)
        {
            var accent = GetAccentColor(primaryColor);
            var useLight = string.Equals(themeMode, "Light", StringComparison.OrdinalIgnoreCase)
                           || (string.Equals(themeMode, "System", StringComparison.OrdinalIgnoreCase)
                               && SystemParameters.WindowGlassColor.R + SystemParameters.WindowGlassColor.G + SystemParameters.WindowGlassColor.B > 382);
            var detailsAccent = AdjustColor(accent, useLight ? 0.72f : 1.2f);

            Resources["AccentBrush"] = new SolidColorBrush(detailsAccent);
            Resources["SystemAccentColorPrimaryBrush"] = new SolidColorBrush(detailsAccent);
            Resources["SystemAccentColorSecondaryBrush"] = new SolidColorBrush(AdjustColor(detailsAccent, 0.9f));
            Resources["SystemAccentColorTertiaryBrush"] = new SolidColorBrush(AdjustColor(detailsAccent, 0.8f));

            if (useLight)
            {
                Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9));
                Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
                Resources["LayerFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD7, 0xE2));
                Resources["ControlFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8));
            }
            else
            {
                Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x21, 0x25));
                Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
                Resources["LayerFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x33));
                Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0x44, 0x49, 0x52));
                Resources["ControlFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0x34, 0x38, 0x42));
            }

            Background = (Brush)Resources["ApplicationBackgroundBrush"];
            Foreground = (Brush)Resources["TextFillColorPrimaryBrush"];

            // Keep theme/accent scoped to Settings resources only to avoid side effects on FocusBar transparency.
        }

        private static Color AdjustColor(Color color, float factor)
        {
            byte Scale(byte c) => (byte)Math.Clamp((int)(c * factor), 0, 255);
            return Color.FromArgb(color.A, Scale(color.R), Scale(color.G), Scale(color.B));
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
    }
}
