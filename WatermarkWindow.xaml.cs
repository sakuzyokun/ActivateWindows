using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ActivateWindows;

public enum WatermarkPosition
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft,
    Center,
    Custom
}

public sealed class WatermarkSettings
{
    public string Line1 { get; set; } = "Activate Windows";
    public string Line2 { get; set; } = "Go to Settings to activate Windows.";
    public Color Color { get; set; } = Colors.White;
    public double Opacity { get; set; } = 0.5;
    public double FontSize { get; set; } = 20;
    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    /// <summary>カスタム座標（物理ピクセル。主モニター左上が (0,0) の仮想デスクトップ座標）</summary>
    public double X { get; set; }
    public double Y { get; set; }

    public bool ClickThrough { get; set; } = true;
    public bool Topmost { get; set; } = true;

    /// <summary>true なら全モニターに表示、false なら主モニターのみ（カスタム座標のときは無視）</summary>
    public bool AllMonitors { get; set; } = true;
}

public partial class WatermarkWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    private WatermarkSettings _settings = new();
    private System.Drawing.Rectangle _area; // 配置先モニターの作業領域（物理ピクセル）
    private bool _repositionQueued;

    public WatermarkWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ConfigureWindowStyles(true);

        // 内容が変わってウィンドウサイズが変わったとき、DPI の違うモニターへ移ったときは
        // 基準の角に合わせて配置し直す
        SizeChanged += (_, _) => QueueReposition();
        DpiChanged += (_, _) => QueueReposition();
    }

    /// <param name="area">このウィンドウを置くモニターの作業領域（物理ピクセル）。Custom のときは未使用。</param>
    public void ApplySettings(WatermarkSettings settings, System.Drawing.Rectangle area)
    {
        _settings = settings;
        _area = area;

        Line1.Text = settings.Line1;
        Line2.Text = settings.Line2;
        Line1.FontSize = settings.FontSize;
        Line2.FontSize = Math.Max(8, settings.FontSize * 0.6);

        var brush = new SolidColorBrush(settings.Color);
        brush.Freeze();
        Line1.Foreground = brush;
        Line2.Foreground = brush;
        WatermarkText.Opacity = settings.Opacity;

        // 配置に合わせて文字揃えと余白を変える（カスタムは余白 0 で、指定座標が文字の左上）
        var align = settings.Position switch
        {
            WatermarkPosition.BottomRight or WatermarkPosition.TopRight => TextAlignment.Right,
            WatermarkPosition.Center => TextAlignment.Center,
            _ => TextAlignment.Left
        };
        Line1.TextAlignment = align;
        Line2.TextAlignment = align;
        WatermarkText.Margin = settings.Position == WatermarkPosition.Custom
            ? new Thickness(0)
            : new Thickness(12);

        Topmost = settings.Topmost;
        ConfigureWindowStyles(settings.ClickThrough);
        QueueReposition();
    }

    private void ConfigureWindowStyles(bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var style = GetWindowLong(hwnd, GwlExStyle);
        style |= WsExToolWindow | WsExNoActivate | WsExLayered;
        if (clickThrough) style |= WsExTransparent;
        else style &= ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);
    }

    // レイアウト確定後（Loaded 優先度）に配置する。連続して呼ばれても 1 回にまとめる。
    private void QueueReposition()
    {
        if (_repositionQueued) return;
        _repositionQueued = true;

        Dispatcher.InvokeAsync(() =>
        {
            _repositionQueued = false;
            Reposition();
            Opacity = 1; // 初回配置が済んだので表示
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Win32 の SetWindowPos で物理ピクセル単位に配置する。
    /// WPF の Left/Top（DIP）を使わないので、拡大率やモニターごとの DPI 差の影響を受けない。
    /// </summary>
    private void Reposition()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (_settings.Position == WatermarkPosition.Custom)
        {
            MoveTo(hwnd, (int)Math.Round(_settings.X), (int)Math.Round(_settings.Y));
            return;
        }

        if (!GetWindowRect(hwnd, out var cur)) return;

        // 目的のモニターの外にいるなら、先にそのモニター内へ移す。
        // （WPF に DPI の切り替えを済ませてもらってから、実際のウィンドウサイズを測るため）
        if (!_area.Contains(cur.Left, cur.Top))
        {
            MoveTo(hwnd, _area.Left, _area.Top);
            UpdateLayout();
            if (!GetWindowRect(hwnd, out cur)) return;
        }

        int w = cur.Right - cur.Left;
        int h = cur.Bottom - cur.Top;
        int x, y;

        switch (_settings.Position)
        {
            case WatermarkPosition.BottomLeft:
                x = _area.Left;
                y = _area.Bottom - h;
                break;
            case WatermarkPosition.TopRight:
                x = _area.Right - w;
                y = _area.Top;
                break;
            case WatermarkPosition.TopLeft:
                x = _area.Left;
                y = _area.Top;
                break;
            case WatermarkPosition.Center:
                x = _area.Left + (_area.Width - w) / 2;
                y = _area.Top + (_area.Height - h) / 2;
                break;
            default: // BottomRight
                x = _area.Right - w;
                y = _area.Bottom - h;
                break;
        }

        MoveTo(hwnd, x, y);
    }

    private static void MoveTo(IntPtr hwnd, int x, int y) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
}