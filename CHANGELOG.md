# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-10-28

### Added
- Tabs: **Remote** (everything that was already there), **Numbers & colors**, **Media**, **Inputs** and **More**. Language, About, the TV IP and the status line stay visible on every tab.
- Number keys 0-9 and Enter, and the Red, Green, Yellow and Blue keys, in the order the TV reports (`system/getColorKeysLayout`; default RGYB).
- Media keys: Play, Pause, Stop, Rewind, Forward, Prev, Next.
- Inputs list built from the TV (`avContent/getCurrentExternalInputsStatus`) with the titles it reports, a "connected" sign and a Refresh button. Clicking an input switches to it with `setPlayContent` (needs pairing). Unpaired, HDMI1-HDMI4 use the IRCC codes and the other inputs show "Not paired".
- Exact volume: a slider from 0 to the TV's `maxVolume` with the current number beside it. The volume is read when the More tab opens and after Vol -, Vol + and Mute; `setAudioVolume` is sent only when the slider is released (needs pairing). Unpaired, the slider is read-only.
- Type text on the TV (needs pairing): reads `getApplicationStatusList` first and says "Open a text field on the TV first" when `textInput` is off; otherwise sends `setTextForm`.
- Sync TV clock (needs pairing): `setCurrentTime` with the PC time. On the KDL-42W805A `dateTime` must be UTC with a `Z` suffix.
- Extra keys on More: Guide (`EPG`, else `GGuide`), Subtitle, Audio, Wide, Exit, Picture off, Sync menu.
- A TV that answers error 12, 14 or 15 (or HTTP 404) gets "Not supported by this TV" instead of a generic error.
- The built-in IRCC table (`IrccCodes.Fallback`) now has all the new codes, read from the KDL-42W805A on 2026-10-28.

### Not included on purpose
- `deleteContent`, `setDeleteProtection`, `setLanguage`, `setPostalCode`, `setWolMode`, `setPowerSavingMode`: they delete recordings or change TV settings. `setWolMode` could turn off Wake-on-LAN, the only way to power this TV on.
- The Japanese keys (BS, CS, Ddata, acTVila, Analog, Digital) and the Netflix, InternetVideo and InternetWidgets keys (services shut down).

## [1.1.1] - 2026-10-28

### Fixed
- Wake-on-LAN MAC and pairing are now stored per TV (by IP); switching the IP no longer wakes or sends the cookie to the previous TV.

### Changed
- `settings.json` keeps the MAC and pairing under `Tvs`, one entry per IP. A 1.1.0 file is migrated automatically to the saved IP, keeping the current pairing.
- Power On with no MAC saved for the current IP asks that TV for it (`system/getSystemSupportedFunction`) before giving up.
- An unreadable `settings.json` is copied to `settings.json.bad` before the app falls back to defaults.

## [1.1.0] - 2026-10-28

### Added
- Version number in the window title and an About window with the version history.
- Language selector: English (US) and Português (Brasil). The choice and the last TV IP are saved in `%APPDATA%\SonyTvRemote\settings.json`.
- Pair with TV: PIN pairing (`accessControl/actRegister`). The auth cookie is stored encrypted with Windows DPAPI and renewed automatically, without a PIN, when less than 2 days remain.
- Power On via Wake-on-LAN, using the MAC the TV reports (`system/getSystemSupportedFunction`).
- Display button.

### Changed
- IRCC codes now come from the TV itself (`system/getRemoteControllerInfo`), with a built-in table of the official codes as a fallback.
- Power On: Wake-on-LAN first; if the TV is still in standby after 6 s, `setPowerStatus` (when paired) and IRCC Power ON; status checked for up to 20 s. On the KDL-42W805A only Wake-on-LAN turns the TV on: `setPowerStatus` answers error 15 "power on not supported" and IRCC Power ON is ignored in standby.
- Open on TV requires pairing and adds `https://` when the URL has no scheme.
- PowerShell script: `TvStatus` uses `system/getPowerStatus` and `TvInfo` uses `system/getInterfaceInformation` (no pairing needed). `OpenPage` and `VolSet` are documented as requiring pairing.

### Fixed
- Options sent the timer code; it now sends the official Options code.
- Back sent "Exit"; it now sends "Return".
- Display code in the PowerShell script.
- Open on TV always failed with HTTP 403 because the TV requires pairing.

## [1.0.0] - 2026-10-28

### Added
- Initial version, never published as a release: Windows GUI remote (Avalonia) with power, volume, D-pad, inputs, channels and "open a URL on the TV", plus the PowerShell script.

[1.2.0]: https://github.com/FabioRueda/sony-bravia-ircc-control/releases/tag/v1.2.0
[1.1.1]: https://github.com/FabioRueda/sony-bravia-ircc-control/commit/a8ddbfd
[1.1.0]: https://github.com/FabioRueda/sony-bravia-ircc-control/commit/d43f6f0
[1.0.0]: https://github.com/FabioRueda/sony-bravia-ircc-control/commit/047a63b
