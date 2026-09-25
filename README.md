# ITBees.LedDisplay

Driver for LED information boards and variable-message traffic signs (VMS) that speak the
**Elitel** line protocol over TCP. The library builds frames (texts fitted to a box, zone layouts,
symbols drawn with pixels), keeps a registry of boards (change detection, retries, probing,
periodic refresh) and reports their online/offline state.

It depends on neither FAS nor EF - only on `Microsoft.Extensions.*` - so it fits applications
running on devices (e.g. ColumnApp in Octopark).

- text with Polish characters (Windows-1250), automatic font choice, wrapping, alignment,
- symbols: arrows ↑ ↓ ← →, a "lane closed" cross, the **B-2** sign (no entry), with or without a caption,
- zone layouts (`LedLayout`) for boards with several counter segments,
- `ILedDisplayManager`: never sends the same frame twice, retries sending to an offline board,
  probes an online board, re-sends the content from time to time (a board is blank after a power
  loss), raises an event on every state change,
- a board emulator (TCP) with an ASCII preview and a console to drive a real board.

## Elitel protocol

TCP, port **3000** (IP and port are set in the board's web console). Every command is one line
terminated with `\n`; the board answers `OK\n`.

| Command | Effect |
|---|---|
| `SET;y;x;"text"` | text in the current font and colour; `x`,`y` = top-left corner of the first character, **row first** |
| `CLEAR` | clears the screen |
| `COLOR;c` | colour 1–7 (1 R, 2 G, 4 B, sums give mixed colours); **immediately recolours all content on the screen** |
| `FONT;n` | 0: 4×6, 1: 6×8, 2: 8×8, 3: 8×16, 4: 12×24 (character cell width×height) |
| `BRIGHT;n` | brightness 1–9, 10 = automatic |
| `PIX;y;x;c` | a single pixel - **not in the manufacturer's description**, known from the test script |

Findings from the manufacturer's test script (`textProt-test.ptp`, Docklight):

- text is sent in **Windows-1250** (`ą` = `B9`, `Ś` = `8C`, `Ż` = `AF`), in plain quotes `0x22`
  (the e-mail shows "typographic" quotes - an editor artefact),
- command names also work in lower case (`set`, `color`, `pix`),
- `pix;0;0;7`, `pix;31;0;6`, `pix;31;63;4` are the corners of the **64×32** test board - `y;x` order as in `SET`,
- a character that does not fit on the screen entirely is not drawn - that is why the library fits
  the text itself instead of sending it and losing it.

The protocol defines no escaping, so `"` in a text becomes `'`, `;` becomes `,`, and control
characters (e.g. a new line) become a space. Characters outside Windows-1250 become `?`.

## Quick start

`appsettings.json`:

```json
{
  "LedDisplays": {
    "ProbeInterval": "00:01:00",
    "RefreshInterval": "00:15:00",
    "RetryInterval": "00:00:15",
    "Displays": [
      { "Id": "entry-sign", "Name": "B-2 in front of the entry", "Host": "192.168.1.50", "Port": 3000, "Width": 64, "Height": 32 },
      { "Id": "lane-entry", "Name": "Above the entry lane", "Host": "192.168.1.51", "Width": 32, "Height": 32 }
    ]
  }
}
```

Registration (singleton `ILedDisplayManager` + a background service):

```csharp
services.AddLedDisplays(configuration.GetSection(LedDisplayOptions.SectionName));
```

Usage (the sign texts are Polish - that is what the boards show):

```csharp
// full-screen message - the largest font it fits in, with wrapping
await ledDisplayManager.ShowAsync("entry-sign",
    LedSigns.Message(64, 32, "PARKING ZAMKNIĘTY", LedColor.Red));

// B-2 with a caption (next to the sign on a wide board, below it on a square one)
await ledDisplayManager.ShowAsync("entry-sign",
    LedSigns.Symbol(64, 32, LedSymbol.NoEntry, LedColor.Red,
        "NIE DOTYCZY POJAZDÓW Z REZERWACJĄ", captionColor: LedColor.White));

// lane signal
await ledDisplayManager.ShowAsync("lane-entry", LedSigns.Symbol(32, 32, LedSymbol.Cross, LedColor.Red));
```

`ShowAsync` does not throw when a board fails - it returns `LedDisplayStatus` (state, last error,
what is shown, what is waiting to be sent) and the frame is retried in the background. It throws
only on a programming error (unknown board, a frame of a different size than the board).

The set of boards can be replaced on the fly (`Configure`) - e.g. after a configuration change in
a panel. Boards with an unchanged definition keep their state.

## Building frames

A frame (`LedFrame`) is always drawn from a clean screen: `BRIGHT` (optional), `CLEAR`, `COLOR`,
texts (`FONT` only when it changes), pixels. The board has one current colour for all text, so a
frame has one `Color`; pixels carry their own colour.

```csharp
var frame = new LedFrameBuilder(96, 32)
    .WithColor(LedColor.Green)
    .WithBrightness(LedBrightness.Auto)
    .Text(0, 0, "-2", LedFont.Font8x16)                              // exact position (throws when it does not fit)
    .TextBox(new LedRect(32, 0, 64, 16), "137",                       // fitted to the box
             LedFont.Font8x16, LedHorizontalAlignment.Right)
    .Symbol(new LedRect(0, 16, 16, 16), LedSymbol.ArrowDown, LedColor.Green)
    .Build();
```

`TextBox` tries fonts from the given one downwards and takes the largest one that fits the whole
text without splitting words; only when that is impossible does it split words, and as a last
resort it truncates.

Multi-segment boards (e.g. "level / general / disabled / EV") are easiest to describe with a
configurable layout:

```csharp
var layout = new LedLayout
{
    Width = 96, Height = 32, Color = LedColor.Green,
    Zones =
    {
        new LedZone { X = 0,  Y = 0, Width = 32, Height = 16, Text = "-2" },
        new LedZone { Key = "general",  X = 32, Y = 0, Width = 32, Height = 16 },
        new LedZone { Key = "disabled", X = 64, Y = 0, Width = 32, Height = 16 },
    },
};
var frame = LedLayoutRenderer.Render(layout, new Dictionary<string, string?> { ["general"] = "137", ["disabled"] = "4" });
```

Symbols are drawn with pixels (the board has no built-in graphics) - a few hundred `PIX` commands
acknowledged one by one, so changing a sign takes noticeably longer than changing a number
(usually under a second or two on a local network).

## Emulator and test console

Board emulator (answers like a board, draws the screen in ASCII):

```bash
dotnet run --project ITBees.LedDisplay.DeviceEmulator -- 3000 64 32
```

Console to drive a real board (instead of Docklight - it encodes Windows-1250 itself and lays out
text like the library; logs every TX/RX line):

```bash
dotnet run --project ITBees.LedDisplay.TestConsole -- 192.168.1.50 64x32 text "PARKING ZAMKNIĘTY" Red
dotnet run --project ITBees.LedDisplay.TestConsole -- 192.168.1.50 64x32 symbol NoEntry Red "TYLKO REZERWACJE"
dotnet run --project ITBees.LedDisplay.TestConsole -- 192.168.1.50 64x32 raw "FONT;3" "SET;0;0;\"123\""
dotnet run --project ITBees.LedDisplay.TestConsole -- 192.168.1.50 64x32 demo
```

Diagnostics in an application: the `Debug` level for the `ITBees.LedDisplay` category logs every
TX/RX line.

## To be confirmed on a physical board / with the manufacturer

1. **Pixel colours vs `COLOR`.** `COLOR` recolours "all current content" - it is unknown whether
   that includes pixels from `PIX`, or whether `PIX` really honours its own colour. That is why the
   B-2 bar is *dark* by default (a red disc with a dark bar), not white - the sign reads well either way.
2. **Error response.** The description only mentions `OK`. The library treats any other answer as
   a rejection and no answer within `CommandTimeout` (2 s) as a failure.
3. **Number of connections.** The library connects for one frame and disconnects right away; if the
   board accepts only one connection, this does not block the web console or service tools.
4. **Fonts 5–6.** The description lists slots 5 and 6 without sizes - not supported.
5. **Screen size of the target boards** (P10 modules, 32×16) and whether the segments of the board
   in front of the entry are one controller (one IP address) or separate ones - the zone layout
   handles both cases.

## Use in Octopark

Octopark uses the library in the **traffic controller** column type (DeviceType 11,
`TrafficControllerColumnApplication` in Octopark.Bl, run by ColumnApp on the parking server). The
boards are its child devices (12 - variable-message LED sign, 13 - LED information board). Their
content follows the traffic organisation (normal / entry only / exit only) and the parking mode;
the operator and the admin can force content on a chosen board - details in
`Octopark/docs/TrafficController.md`.
