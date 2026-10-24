using System.Windows.Threading;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace ActivateWindows;

/// <summary>
/// ウォーターマークのウィンドウを管理する。
/// 「全モニター」ならモニターごとに 1 枚、それ以外なら 1 枚だけ表示する。
/// モニターの抜き差しや解像度変更にも追従する。
/// </summary>
public sealed class WatermarkManager : IDisposable
{
    private readonly List<WatermarkWindow> _windows = new();
    private readonly DispatcherTimer _displayTimer;
    private WatermarkSettings? _settings;
    private bool _disposed;

    public bool IsShowing => _windows.Count > 0;

    public WatermarkManager()
    {
        // 表示設定の変更通知は連続して来るので、少し待ってからまとめて処理する。
        // （Screen クラス側の内部キャッシュ更新を待つ意味もある）
        _displayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _displayTimer.Tick += (_, _) =>
        {
            _displayTimer.Stop();
            if (!_disposed && IsShowing) Sync();
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void Show(WatermarkSettings settings)
    {
        _settings = settings;
        Sync();
    }

    public void Hide()
    {
        foreach (var w in _windows)
            w.Close();
        _windows.Clear();
    }

    private void Sync()
    {
        if (_settings is null) return;

        var areas = GetTargetAreas(_settings);

        while (_windows.Count > areas.Count)
        {
            var last = _windows[^1];
            _windows.RemoveAt(_windows.Count - 1);
            last.Close();
        }

        while (_windows.Count < areas.Count)
        {
            var w = new WatermarkWindow();
            w.Show();
            _windows.Add(w);
        }

        for (int i = 0; i < areas.Count; i++)
            _windows[i].ApplySettings(_settings, areas[i]);
    }

    // 各モニターの作業領域（タスクバーを除いた範囲）を物理ピクセルで返す。
    // カスタム座標のときは 1 枚だけなので、ダミーの空領域を 1 つ返す。
    private static List<System.Drawing.Rectangle> GetTargetAreas(WatermarkSettings settings)
    {
        var result = new List<System.Drawing.Rectangle>();

        if (settings.Position == WatermarkPosition.Custom)
        {
            result.Add(System.Drawing.Rectangle.Empty);
            return result;
        }

        if (settings.AllMonitors)
        {
            foreach (var screen in WinForms.Screen.AllScreens)
                result.Add(screen.WorkingArea);
        }
        else
        {
            var primary = WinForms.Screen.PrimaryScreen ?? WinForms.Screen.AllScreens[0];
            result.Add(primary.WorkingArea);
        }

        return result;
    }

    // SystemEvents は UI スレッド以外から呼ばれるので Dispatcher 経由で処理する
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _displayTimer.Dispatcher.InvokeAsync(() =>
        {
            _displayTimer.Stop();
            _displayTimer.Start();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _displayTimer.Stop();
        Hide();
    }
}