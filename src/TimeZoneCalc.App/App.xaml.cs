using Microsoft.UI.Xaml;

namespace TimeZoneCalc;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // WinUI 崩潰時不會有任何畫面，留一份紀錄方便在別台電腦上查
        UnhandledException += (_, e) =>
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "TimeZoneCalc-crash.txt"), e.Exception.ToString());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
