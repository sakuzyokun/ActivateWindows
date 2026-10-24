using System.Windows;

namespace ActivateWindows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // OSの言語設定に合わせて自動適用
        LanguageManager.AutoSelectLanguage();
    }
}
