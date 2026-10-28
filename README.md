# Network control for Sony BRAVIA TVs (IRCC-IP + Scalar API)

Control a **Sony BRAVIA from the 2010–2013 generation** (Linux/Opera system,
**not** Android TV) over the local network: power off, volume, switch input,
navigate, power on with Wake-on-LAN and, after pairing, open a web page on the
TV's browser — all over HTTP/UDP, with no sideload and no root.

Tested on a **KDL-42W805A** (2013, firmware PKG4.612BRA). Other BRAVIA TVs of
that line that expose the IRCC-IP service may work the same way, but only the
KDL-42W805A was tested.

Two ways to use it:

- **SonyTvRemote** — a Windows app with clickable buttons (English/Portuguese).
  See [Windows app](#windows-app-sonytvremote).
- **`sony-tv-control.ps1`** — PowerShell functions. See
  [Quick start](#quick-start-windows--powershell).

Version history: [CHANGELOG.md](CHANGELOG.md).

## Why this works (and what doesn't)

These TVs run AOSP-based firmware (Android Open Source Project), but they are
**not Android TV**: no Google Play, no APK install, no enablable ADB. What
they do expose on the network is **IRCC-IP** (remote control over IP, SOAP
protocol) and the **Scalar Web API** (JSON-RPC).

What was tested on the KDL-42W805A (2026-10-28):

| Goal | Works? | Note |
|---|---|---|
| Power off, volume up/down, mute, input, arrows | ✅ | IRCC-IP, port 80, no pairing |
| Power on from standby | ✅ Wake-on-LAN only | IRCC Power ON is ignored in standby; `setPowerStatus` answers error 15 |
| Read state (power, volume, inputs) | ✅ | Scalar Web API, no pairing |
| Set an exact volume (`setAudioVolume`) | ⚠️ Pairing required | Unpaired: HTTP 403 with `auth_url`. Paired: worked (set +1, read back, restored) |
| Switch input (`setPlayContent`) | ⚠️ Pairing required | Paired: HDMI 1 and back worked. Unpaired: only the IRCC HDMI1–4 codes |
| Set the TV clock (`setCurrentTime`) | ⚠️ Pairing required | `dateTime` must be UTC with a `Z` suffix; see [Windows app](#windows-app-sonytvremote) |
| Type text (`setTextForm`) | ⚠️ Pairing required | Only with a text field open on the TV (`textInput` is `on`). The success path was not tested without someone at the TV |
| Open a URL on the TV's browser | ⚠️ Pairing required | Works from the app after pairing (`https://example.com` opened). The script cannot pair |
| Install a new app (APK/sideload) | ❌ | Not Android TV |
| ADB over network | ❌ | Port 5555 closed |

## ⚠️ Security warning

On this TV, IRCC-IP accepts remote-control commands **without a password and
without registration**: any device on the same network can press the TV's
buttons. At home, low risk. In a reception area or commercial environment,
**separate the guest Wi-Fi from the equipment network.**

Methods that change settings or open pages (`setActiveApp`, `setAudioVolume`,
…) require pairing with a PIN shown on the TV.

## Requirements

- The TV and the computer on the **same local network**.
- On the TV: `Menu → Settings → Network → Home Network Setup →
  Remote Device / Renderer` turned on (`Yes`).
- Wake-on-LAN worked on the test TV with its default network settings. If it
  doesn't on yours, look for a "Remote Start" option in the TV's network
  settings (not tested).
- Windows. The app is a self-contained `.exe`; the script needs PowerShell.
  The HTTP calls work from any language.

## Windows app (SonyTvRemote)

A clickable remote window under [`app/`](app/), built with
[Avalonia](https://avaloniaui.net/) so it **cross-compiles to Windows from any
OS** (Linux included).

- **Tabs** (1.2.0). The language selector, About, the TV IP / Pair row and the
  status line stay visible on every tab:

  | Tab | What is on it | Needs pairing? |
  |---|---|---|
  | **Remote** | Power On, Power Off, Mute, Vol −/+, Ch −/+, D-pad + OK, Back, Home, Options, Display, Input, HDMI1–HDMI4, "Open on TV" for a URL | Only "Open on TV" |
  | **Numbers & colors** | Keys 0–9 and Enter; Red, Green, Yellow, Blue in the order the TV reports (`system/getColorKeysLayout`, default RGYB) | No |
  | **Media** | Play, Pause, Stop, Rewind, Forward, Prev, Next | No |
  | **Inputs** | List built from `avContent/getCurrentExternalInputsStatus` with the titles the TV reports and a ● when something is connected; Refresh button | Reading: no. Switching: yes (`setPlayContent`); unpaired, HDMI1–HDMI4 still work through their IRCC codes and the other inputs say "Not paired" |
  | **More** | Guide, Subtitle, Audio, Wide, Exit, Picture off, Sync menu; exact volume slider; type text; Sync TV clock | Keys: no. Volume slider (set), type text, Sync TV clock: yes |

- **Exact volume:** a slider from 0 to the TV's `maxVolume` with the current
  number beside it. The volume is read (no pairing) when the More tab opens and
  after every Vol −, Vol + and Mute click. `setAudioVolume` is sent only when
  the slider is **released**, not on every pixel. Unpaired, the slider is
  read-only and the tab says "Not paired — click Pair with TV".
- **Type text:** the app asks `appControl/getApplicationStatusList` first. If
  `textInput` is `off` it shows "Open a text field on the TV first" and does
  **not** call `setTextForm`. If it is `on` (and the app is paired) it sends the
  text with `setTextForm`.
- **Sync TV clock:** `setCurrentTime` with the PC's time. On the KDL-42W805A
  `dateTime` has to be **UTC with a `Z` suffix** (`2026-10-28T12:42:21Z`); local
  times with an offset (`-03:00`, `-0300`) or none are refused with error 3
  "Illegal argument". The TV keeps its own time zone, so the offsets in the
  call are only informative. A TV that has never been set answers error 7
  "Clock is not set" to `getCurrentTime` until this runs.
- **Not supported by this TV:** if the TV answers error 12 (no such method), 14
  (unsupported version) or 15 (unsupported operation), or HTTP 404, the app
  shows "Not supported by this TV" and keeps working.
- **IRCC codes from the TV itself:** on the first button press the app reads
  `system/getRemoteControllerInfo` and uses the TV's own codes by name
  (Back sends `Return`, not `Exit`; Guide sends `EPG`, or `GGuide` if the TV has
  no `EPG`). If that call fails, it uses a built-in table of the official
  KDL-42W805A codes, which now also holds the 1.2.0 keys. HDMI1–HDMI4 are not
  in the TV's list and use the standard BRAVIA codes.
- **Pair with TV:** see [Pairing](#pairing-pin). Needed for "Open on TV", the
  exact volume, switching inputs from the list, typing text and syncing the
  clock. Unpaired, the app shows "Not paired — click Pair with TV".
- **Power On:** see [Power on with Wake-on-LAN](#power-on-with-wake-on-lan).
- **Language selector:** English (US) (default) or Português (Brasil). The
  switch applies immediately to every text, including the status line and the
  About window.
- **About:** version, repository link and the version history, in the selected
  language.
- **Open on TV:** a URL typed without `http://` or `https://` gets `https://`.
- **Settings:** language, last TV IP, the Wake-on-LAN MAC and the pairing data
  are saved in `%APPDATA%\SonyTvRemote\settings.json`. Without that file the
  IP defaults to `192.168.1.100`. The pairing cookie is encrypted with Windows
  DPAPI (current user only).
- **One entry per TV:** the Wake-on-LAN MAC and the pairing are stored per TV,
  keyed by its IP. Switching the IP switches to that TV's own data: Power On
  uses the MAC saved for that IP, and the cookie saved for that IP is the only
  one sent to it. A settings file from 1.1.0 is migrated automatically to the
  saved IP.

### What the app deliberately does not include

- `deleteContent`, `setDeleteProtection`: they delete recordings or change their
  protection.
- `setLanguage`, `setPostalCode`, `setPowerSavingMode`: they change TV settings.
- `setWolMode`: it can turn off Wake-on-LAN, which is the **only** way to power
  this TV on from standby. Without Wake-on-LAN it could only be turned on with
  the original remote.
- The Japanese keys (BS, CS, Ddata, acTVila, Analog, Digital): they do nothing
  on a Brazilian TV.
- Netflix, InternetVideo and InternetWidgets: those services were shut down.

> **Reserve the TV's IP in your router** (DHCP reservation / static lease). The
> app knows a TV only by its IP. If the TV gets a new IP, the app treats it as a
> different TV: it has to learn the MAC again (with the TV on, or in a standby
> that still answers on port 80) and you have to pair again. And if another
> device later gets the TV's old IP, the app would use the old TV's data for it.

The version number lives only in `<Version>` in
[`app/TvRemote.csproj`](app/TvRemote.csproj); the window title and About
window read it from the assembly.

### Download or build

Published builds of `SonyTvRemote.exe` go on the
[GitHub Releases](https://github.com/FabioRueda/sony-bravia-ircc-control/releases)
page. The `.exe` is not committed (binaries don't belong in git).

Latest: **[v1.2.0](https://github.com/FabioRueda/sony-bravia-ircc-control/releases/tag/v1.2.0)**.
Check the download before running it (PowerShell):

```powershell
Get-FileHash .\SonyTvRemote.exe -Algorithm SHA256
# expected for v1.2.0: AFC25D950278A8EEA17B74ABCB354B55020188C43159A22300A1AA3A36A1137B
```

Testing status of v1.2.0: unit tests pass, and the new Scalar calls (exact
volume, input switch, clock, text-input gate) were checked against the real
KDL-42W805A over the API. The on-screen behavior of the new IRCC keys (numbers,
colors, media, Guide, Subtitle, Audio, Wide, Exit, Picture off, Sync menu) has
not been checked by someone in front of the TV yet. Fixes, if any, will ship as
v1.2.1.

To build it:

```bash
cd app
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o out
# -> out/SonyTvRemote.exe  (self-contained, ~42 MB, no .NET install needed)
```

Requires the .NET 8 SDK. The resulting `.exe` runs on any Windows x64 machine
with nothing else installed. It is unsigned, so Windows SmartScreen will warn
on first run (More info → Run anyway).

Unit tests (version, translations, Wake-on-LAN packet, URL handling, IRCC code
parsing and the built-in table, cookie handling, settings, power-on sequence,
inputs list, volume limits, color-key order, TV clock format, text-input gate,
error classification, request formats):

```bash
dotnet test tests/TvRemote.Tests
```

## Quick start (Windows / PowerShell)

1. Find the TV's IP: `Menu → Network → View network status`.
2. Edit `sony-tv-control.ps1` and set `$global:TV_IP` to your TV's IP
   (example: `192.168.1.100`).
3. Load the functions into the session (the leading dot-space matters):
   ```powershell
   . .\sony-tv-control.ps1
   ```
4. Command by name:
   ```powershell
   PowerOff
   VolUp
   SwitchInput
   HDMI1
   TvStatus
   ```

## Available commands

- **Power:** `PowerOff`, `PowerOn`, `PowerToggle`
- **Sound:** `VolUp`, `VolDown`, `Mute`, `VolGet`, `VolSet <n>` (requires
  pairing)
- **Inputs:** `SwitchInput`, `HDMI1`..`HDMI4`, `ListInputs`
- **Navigation:** `Up`, `Down`, `Left`, `Right`, `Ok`, `Back`, `Home`,
  `Options`, `Display`
- **Channels:** `ChannelUp`, `ChannelDown`
- **State:** `TvStatus` (power status), `TvInfo` (model info)
- **Web:** `OpenPage "<url>"` (requires pairing)

> Power off and IRCC volume were confirmed on the KDL-42W805A. `PowerOn`
> (IRCC) is ignored in standby on that model — use the app's Power On, which
> sends Wake-on-LAN. Back, Options and Display use the official codes from the
> TV's own list (`system/getRemoteControllerInfo`). The script does not pair,
> so `VolSet` and `OpenPage` get HTTP 403 with `auth_url`.

## Pairing (PIN)

Tested on the KDL-42W805A. The app's **Pair with TV** button does this:

1. `POST http://<IP>/sony/accessControl`:
   ```json
   {"method":"actRegister","id":1,"version":"1.0","params":[
     {"clientid":"SonyTvRemote:<GUID>","nickname":"SonyTvRemote (<PC name>)","level":"private"},
     [{"value":"yes","function":"WOL"}]]}
   ```
   The TV answers **HTTP 401** and shows a **4-digit PIN** on screen. The GUID
   is generated once per PC and saved in the settings.
2. The same request again, with `Authorization: Basic base64(":" + PIN)`. The
   TV answers **HTTP 200** and a cookie:
   `Set-Cookie: auth=...; path=/sony/; max-age=1209600` (14 days).
3. Every Scalar call then sends `Cookie: auth=...`.

Renewal: the same `actRegister` with the current cookie and **no** PIN returns
HTTP 200 with a new cookie. The app does this automatically when less than
2 days remain, and only shows "Not paired" if renewal fails.

## Power on with Wake-on-LAN

In standby the KDL-42W805A still answers on port 80 (`getPowerStatus` returns
`"standby"`), but IRCC Power ON returns HTTP 200 and the TV stays off, and
`system/setPowerStatus {"status":true}` (paired) answers
`{"error":[15,"power on not supported"]}`. **Only Wake-on-LAN turns it on.**

The TV reports its MAC without pairing in `system/getSystemSupportedFunction`
(item with `"option":"WOL"`). The app saves it for that TV's IP the first time
it talks to the TV, so it is available when the TV is in standby. If no MAC is
saved for the current IP, Power On asks the TV for it first (the KDL-42W805A
still answers on port 80 in standby).

The app's **Power On**:

1. Sends the magic packet (UDP port 9) to `255.255.255.255` and to the subnet
   broadcast of each active network adapter.
2. Checks `getPowerStatus` every 2 s for up to 6 s.
3. Still in standby: `setPowerStatus {"status":true}` (when paired; error 15 is
   ignored), then IRCC Power ON — for other models that support them.
4. Keeps checking until 20 s in total and shows "On" or "Still in standby".
   It stops as soon as the TV reports `"active"`.

On the test TV it reported `"active"` about 2 s after the magic packet.

## Controlling from any language

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

JSON-RPC body. Example, read the power status (no pairing needed):

```json
{"method":"getPowerStatus","id":1,"version":"1.0","params":[]}
```

Services exposed by this generation: `guide`, `system`, `videoScreen`,
`audio`, `avContent`, `appControl`, `browser`, `notification`, `cec`,
`accessControl`, `irCommandProxy`.

## Discovering YOUR TV's codes and services

- **IRCC codes:** `system/getRemoteControllerInfo` (no pairing) returns the
  TV's full list of button names and codes.
- **Wake-on-LAN MAC:** `system/getSystemSupportedFunction` (no pairing).
- **Device description:** `http://<IP>:52323/dmr.xml` has the
  `X_IRCCCodeList` (Power/Power ON/Power OFF), the Scalar API base
  (`X_ScalarWebAPI_BaseURL`) and the service list.

## Auth-free vs. auth-required Scalar methods (KDL-42W805A)

- **Work without auth:** `system/getPowerStatus`, `system/getInterfaceInformation`,
  `system/getRemoteControllerInfo`, `system/getSystemSupportedFunction`,
  `audio/getVolumeInformation`, `avContent/getCurrentExternalInputsStatus`,
  `avContent/getSourceList`.
- **Require auth** (the TV answers with `auth_url` instead of a result):
  `avContent/getPlayingContentInfo`, `avContent/getContentList`,
  `system/getSystemInformation`, `appControl/getApplicationList`,
  `appControl/setActiveApp`, `audio/setAudioVolume`, `system/setPowerStatus`,
  `system/getWolMode`.

After pairing, `setActiveApp`, `getSystemInformation` and
`getPlayingContentInfo` returned results. `setPowerStatus` accepted the cookie
but answered error 15 (power on not supported).

Inputs returned by `getCurrentExternalInputsStatus`:

| Input | URI |
|-------|-----|
| HDMI 1 | `extInput:hdmi?port=1` |
| HDMI 2 (MHL) | `extInput:hdmi?port=2` |
| HDMI 3 | `extInput:hdmi?port=3` |
| HDMI 4 | `extInput:hdmi?port=4` |
| Video 1 | `extInput:composite?port=1` |
| Video 2/Component | `extInput:composite?port=2` |
| Screen mirroring | `extInput:widi?port=1` |

> IRCC-IP (the remote-control buttons) needs no auth.

## Port scan (reference, KDL-42W805A)

| Port | State | Service |
|------:|--------|---------|
| 80 | open | HTTP: IRCC + Scalar API + DIAL |
| 52323 | open | DLNA/UPnP MediaRenderer |
| 443, 8008, 8009, 7676, 50001, 50002, 2869, 23, 5555 | closed | — |

## License

MIT. See [LICENSE](LICENSE). Use at your own risk; only touch what is yours.
