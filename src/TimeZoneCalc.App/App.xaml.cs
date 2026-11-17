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
        {
            // 規格 §7：畫面上的例外不讓程式整個關掉。不標成已處理的話，WinUI 寫完紀錄就直接結束程序
            e.Handled = true;
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "TimeZoneCalc-crash.txt"), e.Exception.ToString());
            }
            catch
            {
                // TEMP 寫不進去或檔案被鎖：紀錄沒留到，但不能在例外處理裡再丟一次
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
