# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.1.0]: https://github.com/FabioRueda/sony-bravia-ircc-control/releases/tag/v1.1.0
[1.0.0]: https://github.com/FabioRueda/sony-bravia-ircc-control/commit/047a63b
