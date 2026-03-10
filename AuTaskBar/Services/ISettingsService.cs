namespace AuTaskBar.Services
{
    public interface ISettingsService
    {
        FocusBarSettings Load();
        void Save(FocusBarSettings settings);
    }
}
