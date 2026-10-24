using System;
using System.Globalization;
using System.Windows;

namespace ActivateWindows;

public static class LanguageManager
{
    public static void ChangeLanguage(string cultureCode)
    {
        var dict = new ResourceDictionary();

        switch (cultureCode)
        {
            case "en-US":
            case "en":
                dict.Source = new Uri("Resources/Strings.en-US.xaml", UriKind.Relative);
                break;
            default:
                dict.Source = new Uri("Resources/Strings.ja-JP.xaml", UriKind.Relative);
                break;
        }

        // 既存の言語リソースを削除して差し替え
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
    }

    // OSの言語に合わせて自動設定
    public static void AutoSelectLanguage()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        if (currentCulture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            ChangeLanguage("ja-JP");
        }
        else
        {
            ChangeLanguage("en-US"); // 日本語以外は英語にする
        }
    }

    // C# コードから文字列リソースを取得する便利メソッド
    public static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? string.Empty;
    }
}