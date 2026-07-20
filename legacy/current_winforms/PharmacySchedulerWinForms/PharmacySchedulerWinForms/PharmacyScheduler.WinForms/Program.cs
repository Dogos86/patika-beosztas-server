using PharmacyScheduler.WinForms.Infrastructure;
using QuestPDF.Infrastructure;

namespace PharmacyScheduler.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        ApplicationConfiguration.Initialize();

        var store = new AppDataFileStore(Path.Combine(AppContext.BaseDirectory, "scheduler-data.json"));
        Application.Run(new MainForm(store));
    }
}
