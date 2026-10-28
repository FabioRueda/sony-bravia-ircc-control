# Network control for Sony BRAVIA TVs (IRCC-IP + Scalar API)

Control a **Sony BRAVIA from the 2010–2013 generation** (Linux/Opera system,
**not** Android TV) over the local network: power on/off, volume, switch
input, navigate, and even open a web page on the TV's browser — all over
HTTP, with no app, no sideload, no root.

Validated on a **KDL-42W805A** (2013, firmware PKG4.612BRA). The same method
works on other BRAVIA TVs of that line that expose the IRCC-IP service.

## Why this works (and what doesn't)

These TVs run AOSP-based firmware (Android Open Source Project), but they are
**not Android TV**: no Google Play, no APK install, no enablable ADB. What
they do expose on the network is **IRCC-IP** (remote control over IP, SOAP
protocol) and the **Scalar Web API** (JSON, fine-grained state control).

An honest summary of what each path delivers:

| Goal | Works? | Note |
|---|---|---|
| Control over network (power, volume, input, arrows) | ✅ | IRCC-IP, port 80 |
| Read/set exact state (volume=10, current input) | ✅ | Scalar Web API |
| Open a URL on the TV's browser | ✅ | `appControl/setActiveApp` |
| Stream media from PC/phone | ✅ | DLNA/DMR, port 52323 |
| Install a new app (APK/sideload) | ❌ | Not Android TV |
| ADB over network | ❌ | Port 5555 closed, no developer options |
| Root | ❌ | Only via UART (hardware), brick risk, no payoff |

## ⚠️ Security warning

On this generation, IRCC-IP usually accepts commands **without a password and
without registration**: any device on the same network can control the TV. At
home, low risk. In a reception area or commercial environment, **separate the
guest Wi-Fi from the equipment network.**

## Requirements

- The TV and the computer on the **same local network**.
- On the TV: `Menu → Settings → Network → Home Network Setup →
  Remote Device / Renderer` turned on (`Yes`). "Remote Start" enabled also
  enables Wake-on-LAN.
- Windows with PowerShell (the script is `.ps1`). The HTTP method works from
  any language; PowerShell is just the wrapper.

## Quick start (Windows / PowerShell)

1. Find the TV's IP: `Menu → Network → View network status`.
2. Edit `sony-tv-control.ps1` and set `$global:TV_IP` to your TV's IP.
3. Load the functions into the session (the leading dot-space matters):
   ```powershell
   . .\sony-tv-control.ps1
   ```
4. Command by name:
   ```powershell
   PowerOn
   PowerOff
   VolSet 10
   SwitchInput
   HDMI1
   TvStatus
   OpenPage "https://example.com"
   ```

## Available commands

- **Power:** `PowerOn`, `PowerOff`, `PowerToggle`
- **Sound:** `VolUp`, `VolDown`, `Mute`, `VolSet <n>`, `VolGet`
- **Inputs:** `SwitchInput`, `HDMI1`..`HDMI4`, `ListInputs`
- **Navigation:** `Up`, `Down`, `Left`, `Right`, `Ok`, `Back`, `Home`,
  `Options`, `Display`
- **Channels:** `ChannelUp`, `ChannelDown`
- **State:** `TvStatus`, `TvInfo`
- **Web:** `OpenPage "<url>"`

> Power on/off and volume were confirmed on the KDL-42W805A. The navigation
> codes are the standard BRAVIA ones; if one doesn't react on your model, the
> official codes are in the TV's own `dmr.xml` (see below).

## Windows GUI app (SonyTvRemote)

A small clickable remote window is included under [`app/`](app/): power, volume,
D-pad, inputs, channels, and "open a URL on the TV". It is built with
[Avalonia](https://avaloniaui.net/), so it **cross-compiles to Windows from
any OS** (Linux included) — unlike WinForms/WPF, it does not need the
WindowsDesktop SDK.

The compiled `.exe` is not committed (binaries don't belong in git). Build it
yourself:

```bash
cd app
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o out
# -> out/SonyTvRemote.exe  (self-contained, ~42 MB, no .NET install needed)
```

Requires the .NET 8 SDK. The resulting `.exe` is self-contained: it runs on
any Windows x64 machine with nothing else installed. It is unsigned, so
Windows SmartScreen will warn on first run (More info → Run anyway).

## Controlling from any language (not just PowerShell)

### IRCC-IP (remote control) — `POST http://<IP>/sony/IRCC`

SOAP body, header `SOAPAction: "urn:schemas-sony-com:service:IRCC:1#X_SendIRCC"`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
<s:Body><u:X_SendIRCC xmlns:u="urn:schemas-sony-com:service:IRCC:1">
<IRCCCode>AAAAAQAAAAEAAAASAw==</IRCCCode>
</u:X_SendIRCC></s:Body></s:Envelope>
```

### Scalar Web API — `POST http://<IP>/sony/<service>`

JSON-RPC body. Example, set volume to 10:

```json
{"method":"setAudioVolume","id":1,"version":"1.0","params":[{"target":"speaker","volume":"10"}]}
```

Services exposed by this generation: `guide`, `system`, `videoScreen`,
`audio`, `avContent`, `appControl`, `browser`, `notification`, `cec`,
`accessControl`, `irCommandProxy`.

## Discovering YOUR TV's codes and services

The TV itself publishes the service list and some IRCC codes at:

```
http://<IP>:52323/dmr.xml
```

There you'll find the `X_IRCCCodeList` (with Power/Power ON/Power OFF), the
Scalar API base (`X_ScalarWebAPI_BaseURL`), and the service list. Other useful
endpoints:

- IRCC: `http://<IP>/sony/IRCC`
- App list (DIAL): `http://<IP>/DIAL/sony/applist`

## Port scan (reference, KDL-42W805A)

| Port | State | Service |
|------:|--------|---------|
| 80 | open | HTTP: IRCC + Scalar API + DIAL |
| 52323 | open | DLNA/UPnP MediaRenderer |
| 443, 8008, 8009, 7676, 50001, 50002, 2869, 23, 5555 | closed | — |

## License

MIT. See [LICENSE](LICENSE). Use at your own risk; only touch what is yours.
