using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ActivateWindows;

public partial class MainWindow : Window
{
    private readonly WatermarkManager _manager;
    private readonly TrayIconService _tray;
    private bool _exiting;
    private bool _cleanedUp;
    private bool _balloonShown;

    public MainWindow()
    {
        InitializeComponent();

        // トレイ常駐のため、設定画面を閉じてもアプリ自体は終了しない（終了は ExitApp から）
        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        System.Windows.Application.Current.Exit += (_, _) => Cleanup();

        _manager = new WatermarkManager();
        _tray = new TrayIconService(ShowSettings, ToggleWatermark, ExitApp);

        Closing += MainWindow_Closing;
        PositionBox.SelectedIndex = 0;
        UpdatePositionFieldsEnabled();
    }

    // ---- ウォーターマークの表示・非表示 -------------------------------------------------

    private void ShowWatermark(string status)
    {
        if (!TryBuildSettings(out var settings))
            return;

        _manager.Show(settings);
        _tray.SetWatermarkVisible(true);
        StatusText.Text = status;
    }

    private void HideWatermark()
    {
        _manager.Hide();
        _tray.SetWatermarkVisible(false);
        StatusText.Text = LanguageManager.GetString("StatusHide");
    }

    private void ToggleWatermark()
    {
        if (_manager.IsShowing) HideWatermark();
        else ShowWatermark(LanguageManager.GetString("StatusShow"));
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
        => ShowWatermark(LanguageManager.GetString("StatusPreview"));

    private void Apply_Click(object sender, RoutedEventArgs e)
        => ShowWatermark(LanguageManager.GetString("StatusApplied"));

    private void CloseOverlay_Click(object sender, RoutedEventArgs e) => HideWatermark();

    // ---- トレイ・終了処理 ---------------------------------------------------------------

    private void ShowSettings()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        Cleanup();
        // Closing の途中から呼ばれることもあるので、Shutdown は少し後ろに回す
        Dispatcher.InvokeAsync(() => System.Windows.Application.Current.Shutdown());
    }

    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        _manager.Dispose();
        _tray.Dispose();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;

        if (ExitWithSettingsBox.IsChecked == true)
        {
            ExitApp();
            return;
        }

        // 閉じる代わりにトレイへ隠す
        e.Cancel = true;
        Hide();

        if (!_balloonShown)
        {
            _balloonShown = true;
            _tray.ShowBalloon("トレイに常駐中", "ダブルクリックで設定を開けます。終了はトレイのメニューから。");
        }
    }

    // ---- 設定の読み取り -----------------------------------------------------------------

    private bool TryBuildSettings(out WatermarkSettings settings)
    {
        settings = new WatermarkSettings();

        if (!TryParseColor(ColorBox.Text, out var color))
        {
            System.Windows.MessageBox.Show("色は #FFFFFF のような #RRGGBB 形式で入力してね。", "色の入力エラー",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!double.TryParse(FontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize)
            || fontSize < 6 || fontSize > 200)
        {
            System.Windows.MessageBox.Show("文字サイズは 6～200 の数値で入力してね。", "入力エラー",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!double.TryParse(XBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(YBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            System.Windows.MessageBox.Show("X と Y は数値で入力してね。", "座標の入力エラー",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var position = PositionBox.SelectedIndex switch
        {
            1 => WatermarkPosition.BottomLeft,
            2 => WatermarkPosition.TopRight,
            3 => WatermarkPosition.TopLeft,
            4 => WatermarkPosition.Center,
            5 => WatermarkPosition.Custom,
            _ => WatermarkPosition.BottomRight
        };

        settings = new WatermarkSettings
        {
            Line1 = Line1Box.Text,
            Line2 = Line2Box.Text,
            Color = color,
            Opacity = OpacitySlider.Value / 100.0,
            FontSize = fontSize,
            Position = position,
            X = x,
            Y = y,
            ClickThrough = ClickThroughBox.IsChecked == true,
            Topmost = TopmostBox.IsChecked == true,
            AllMonitors = AllMonitorsBox.IsChecked == true
        };
        return true;
    }

    private static bool TryParseColor(string text, out Color color)
    {
        color = Colors.White;
        var value = text.Trim();
        if (value.StartsWith('#')) value = value[1..];
        if (value.Length != 6 || !uint.TryParse(value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var rgb))
            return false;

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    // ---- UI イベント --------------------------------------------------------------------

    private void PositionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        UpdatePositionFieldsEnabled();
    }

    private void UpdatePositionFieldsEnabled()
    {
        if (XBox is null || YBox is null || PositionBox is null || AllMonitorsBox is null) return;
        var custom = PositionBox.SelectedIndex == 5;
        XBox.IsEnabled = custom;
        YBox.IsEnabled = custom;
        AllMonitorsBox.IsEnabled = !custom; // カスタム座標のときは 1 枚だけ表示
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is not null)
            OpacityLabel.Text = $"{OpacitySlider.Value:0}%";
    }

    private void ResetColor_Click(object sender, RoutedEventArgs e) => ColorBox.Text = "#FFFFFF";
}