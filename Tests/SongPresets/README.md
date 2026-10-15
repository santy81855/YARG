# Song preset checks

```sh
dotnet run --project Tests/SongPresets/SongPresets.Checks.csproj
```

Uses the real preset data, settings helper, pre-song preset handler, and vocals
toggle and shared setup/navigation code with stand-ins for Unity-owned services.
Checks persistence, multiple-player setup flow, audio volume behavior, online
host/guest rules, defaults, bounds, and error feedback.
See [SongPresets.md](../../Docs/SongPresets.md) for behavior and in-game checks.
