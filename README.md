# Activate Windows (WPF)

Windows desktop watermark parody app. It does not change Windows activation status.

## Requirements
- Windows 10/11
- .NET 8 SDK (Windows)
- Visual Studio 2022 with the .NET desktop development workload, or `dotnet` CLI

## Run
From this folder:
```powershell
dotnet run
```

## Publish a standalone folder
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
Output is under `bin\Release\net8.0-windows\win-x64\publish\`.

## Notes
- The overlay is a transparent, borderless WPF window.
- Default color is white with 50% opacity, matching `#FFFFFF80` in CSS RGBA notation.
- WPF's ARGB equivalent for that color is `#80FFFFFF`.
- Click-through is enabled by default; uncheck it if you want to interact with the overlay window.
- The app currently has no tray icon or settings persistence. Use the `表示を終了` button to close the overlay. The checkbox can also make the overlay close when the settings window closes.
