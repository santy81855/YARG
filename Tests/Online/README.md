Run `dotnet run --project Tests/Online/Online.Checks.csproj` with .NET 8 installed.

The checks use the real YARG.Core vocal engine. They cover independent singing
keys, mid-song changes, delayed/batched input delivery, final score serialization,
host clock estimation, and song archives including malformed peer packages.

For a real Unity Transport smoke test, copy `Unity/OnlineTransportSmoke.cs` into
`Assets/OnlineValidation` in a disposable full project snapshot. Run Unity in
batch mode with `-executeMethod YARG.Online.Validation.OnlineTransportSmoke.Build`.
Start the resulting Linux executable twice, using `-batchmode -nographics
-online-smoke -persistent-data-path <separate-temporary-folder>` on both, plus
`-host` on the first instance. Both logs must contain `ONLINE_SMOKE_PASS`.
This checks two real game processes over loopback, including NGO connection
approval, the application handshake, clock sync, and fragmented reliable traffic.
It does not verify Unity Relay account configuration or internet routing.

The remaining live acceptance test uses matching Windows/Linux builds linked to
the same Unity Cloud project. Host creates a room, guest joins from another home,
and guest starts with no copy of the host's song. Check transfer progress, local
instrument selection, synchronized countdown, independent keys, both performances,
pause/resume from either computer, restart, all final score cards, return to room,
and a second song. Disconnect either computer during download and gameplay and
check that both return to usable menus.
