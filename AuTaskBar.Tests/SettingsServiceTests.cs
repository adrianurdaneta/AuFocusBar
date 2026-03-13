using System;
using System.IO;
using AuTaskBar.Services;
using Xunit;

namespace AuTaskBar.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var path = CreateTempSettingsPath();
        var sut = new SettingsService(path);

        var settings = sut.Load();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal(25, settings.PomodoroFocusMinutes);
        Assert.Equal(5, settings.PomodoroRestMinutes);
        Assert.Equal(1280, settings.BarWidth);
        Assert.Equal(90, settings.BarHeight);
        Assert.Equal(0.7, settings.BarOpacity);
    }

    [Fact]
    public void Load_WhenInvalidValues_NormalizesValues()
    {
        var path = CreateTempSettingsPath();
        var invalidJson = """
        {
          "PomodoroFocusMinutes": 0,
          "PomodoroRestMinutes": -1,
          "BarWidth": 100,
          "BarHeight": 10,
          "BarOpacity": 9,
          "MenuPalette": "",
          "AppThemeMode": "",
          "AppTheme": ""
        }
        """;

        File.WriteAllText(path, invalidJson);
        var sut = new SettingsService(path);

        var settings = sut.Load();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal(25, settings.PomodoroFocusMinutes);
        Assert.Equal(5, settings.PomodoroRestMinutes);
        Assert.Equal(1280, settings.BarWidth);
        Assert.Equal(90, settings.BarHeight);
        Assert.Equal(1.0, settings.BarOpacity);
        Assert.Equal("Dark", settings.MenuPalette);
        Assert.Equal("Dark", settings.AppThemeMode);
        Assert.Equal("Ocean", settings.AppTheme);
    }

    [Fact]
    public void Save_ThenLoad_PreservesPositionAndAnchor()
    {
        var path = CreateTempSettingsPath();
        var sut = new SettingsService(path);

        var input = new FocusBarSettings
        {
            AnchorToTop = true,
            WindowLeft = 111,
            WindowTop = 222,
            PomodoroFocusMinutes = 30,
            PomodoroRestMinutes = 10
        };

        sut.Save(input);
        var loaded = sut.Load();

        Assert.True(loaded.AnchorToTop);
        Assert.Equal(111, loaded.WindowLeft);
        Assert.Equal(222, loaded.WindowTop);
        Assert.Equal(30, loaded.PomodoroFocusMinutes);
        Assert.Equal(10, loaded.PomodoroRestMinutes);
    }

    private static string CreateTempSettingsPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AuTaskBar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }
}
