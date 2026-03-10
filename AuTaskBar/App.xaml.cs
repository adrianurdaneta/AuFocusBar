using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AuTaskBar
{
    public partial class App : Application
    {
        private ContextMenuLoader? _contextMenuLoader;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.None, updateAccent: true);
            ApplicationAccentColorManager.ApplySystemAccent();
            _contextMenuLoader = new ContextMenuLoader();
        }
    }
}
