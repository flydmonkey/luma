# Luma

Local-first Windows screen recorder. This repository is a greenfield
rewrite of the previous Luma desktop app: the WinUI shell stays, live
capture and hardware encoding go through open-source **libobs**.

## Requirements

- 64-bit Windows 10 1809 or later
- .NET 8 SDK
- x64

## Build

```powershell
dotnet build src/Luma.App/Luma.App.csproj -c Debug -p:Platform=x64
dotnet test Luma.sln -c Debug -p:Platform=x64
```

The unpackaged executable is:

`src\Luma.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Luma.exe`

## License

GPL-2.0-or-later because the app links [libobs](https://github.com/obsproject/obs-studio).
See `LICENSE`, `COPYING`, and `ThirdPartyNotices`.
