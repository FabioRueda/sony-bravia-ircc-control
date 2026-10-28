<#
=====================================================================
 Network control for Sony BRAVIA TVs (2010-2013 generation)
 IRCC-IP (remote control) + Scalar Web API (fine-grained state)
 No app, no sideload, no root.

 SECURITY WARNING: on this generation IRCC-IP usually accepts commands
 with no password. Any machine on the same network can control the TV.
 In a commercial environment, separate guest Wi-Fi from the equipment
 network.

 Usage:  . .\sony-tv-control.ps1     (loads the functions into the session)
         PowerOff ; VolUp ; SwitchInput ; HDMI1 ; TvStatus
         (VolSet and OpenPage need pairing, which this script doesn't do;
          PowerOn is ignored in standby on the KDL-42W805A - use Wake-on-LAN)
=====================================================================
#>

$global:TV_IP = "192.168.1.100"   # <-- SET this to your TV's IP (Menu > Network > View network status)

# ---------- ENGINE: send a remote control code (IRCC) ----------
function Send-IRCC($code){
  $b = @"
<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
<s:Body><u:X_SendIRCC xmlns:u="urn:schemas-sony-com:service:IRCC:1"><IRCCCode>$code</IRCCCode></u:X_SendIRCC></s:Body></s:Envelope>
"@
  try {
    Invoke-WebRequest -Uri "http://$($global:TV_IP)/sony/IRCC" -Method POST -Body $b -Headers @{
      "SOAPAction"='"urn:schemas-sony-com:service:IRCC:1#X_SendIRCC"'
      "Content-Type"="text/xml; charset=UTF-8"
    } -UseBasicParsing -TimeoutSec 5 | Out-Null
    return $true
  } catch {
    Write-Host "Failed (TV off or off-network?): $($_.Exception.Message)" -ForegroundColor Yellow
    return $false
  }
}

# ---------- ENGINE: call the Scalar Web API (state and fine control) ----------
function Send-Scalar($service,$json){
  try {
    Invoke-RestMethod -Uri "http://$($global:TV_IP)/sony/$service" -Method POST -Body $json -ContentType "application/json" -TimeoutSec 5
  } catch {
    Write-Host "Scalar API failed: $($_.Exception.Message)" -ForegroundColor Yellow
  }
}

# =====================================================================
#  NAMED COMMANDS (IRCC)
#  Power off and volume confirmed on the KDL-42W805A. Your TV's official
#  codes: system/getRemoteControllerInfo (no auth), or the TV's dmr.xml at
#  http://<IP>:52323/dmr.xml
# =====================================================================
# Power ON: the KDL-42W805A ignores it in standby (HTTP 200, TV stays off).
# Only Wake-on-LAN turns that model on; see README.
function PowerOn     { Send-IRCC "AAAAAQAAAAEAAAAuAw==" }   # Power ON
function PowerOff    { Send-IRCC "AAAAAQAAAAEAAAAvAw==" }   # Power OFF
function PowerToggle { Send-IRCC "AAAAAQAAAAEAAAAVAw==" }   # toggle on/off
function VolUp       { Send-IRCC "AAAAAQAAAAEAAAASAw==" }
function VolDown     { Send-IRCC "AAAAAQAAAAEAAAATAw==" }
function Mute        { Send-IRCC "AAAAAQAAAAEAAAAUAw==" }
function SwitchInput { Send-IRCC "AAAAAQAAAAEAAAAlAw==" }   # cycles inputs
function ChannelUp   { Send-IRCC "AAAAAQAAAAEAAAAQAw==" }
function ChannelDown { Send-IRCC "AAAAAQAAAAEAAAARAw==" }

# Navigation (arrows, OK, back, menu)
function Up       { Send-IRCC "AAAAAQAAAAEAAAB0Aw==" }
function Down     { Send-IRCC "AAAAAQAAAAEAAAB1Aw==" }
function Left     { Send-IRCC "AAAAAQAAAAEAAAA0Aw==" }
function Right    { Send-IRCC "AAAAAQAAAAEAAAAzAw==" }
function Ok       { Send-IRCC "AAAAAQAAAAEAAABlAw==" }
# Back, Options and Display use the official codes from the TV's own list
# (system/getRemoteControllerInfo, checked 2026-10-28)
function Back     { Send-IRCC "AAAAAgAAAJcAAAAjAw==" }   # "Return" (the old ...ABjAw== is "Exit")
function Home     { Send-IRCC "AAAAAQAAAAEAAABgAw==" }
function Options  { Send-IRCC "AAAAAgAAAJcAAAA2Aw==" }   # the old ...AA2Aw== toggled the sleep timer
function Display  { Send-IRCC "AAAAAQAAAAEAAAA6Aw==" }

# Direct HDMI inputs
function HDMI1 { Send-IRCC "AAAAAgAAABoAAABaAw==" }
function HDMI2 { Send-IRCC "AAAAAgAAABoAAABbAw==" }
function HDMI3 { Send-IRCC "AAAAAgAAABoAAABcAw==" }
function HDMI4 { Send-IRCC "AAAAAgAAABoAAABdAw==" }

# =====================================================================
#  FINE CONTROL (Scalar API) - read and write state with exact values
# =====================================================================

# Show whether the TV is on or in standby (getPowerStatus needs no auth;
# getPlayingContentInfo requires auth on this TV)
function TvStatus { Send-Scalar "system" '{"method":"getPowerStatus","id":1,"version":"1.0","params":[]}' }

# Show model name and interface version (getInterfaceInformation needs no auth;
# getSystemInformation requires auth on this TV)
function TvInfo   { Send-Scalar "system" '{"method":"getInterfaceInformation","id":1,"version":"1.0","params":[]}' }

# Read the current volume
function VolGet   { Send-Scalar "audio" '{"method":"getVolumeInformation","id":1,"version":"1.0","params":[]}' }

# Lock the volume at an exact value:  VolSet 10
# NOTE: setAudioVolume requires pairing on the KDL-42W805A (HTTP 403 with "auth_url").
# Unpaired, use VolUp/VolDown (IRCC) instead.
function VolSet($n){ Send-Scalar "audio" "{`"method`":`"setAudioVolume`",`"id`":1,`"version`":`"1.0`",`"params`":[{`"target`":`"speaker`",`"volume`":`"$n`"}]}" }

# List the TV's available inputs
function ListInputs { Send-Scalar "avContent" '{"method":"getCurrentExternalInputsStatus","id":1,"version":"1.0","params":[]}' }

# Open a web page on the TV's browser.
# NOTE: requires pairing. Unpaired, the TV answers HTTP 403 with "auth_url".
# This script does not pair; use the SonyTvRemote app ("Pair with TV").
function OpenPage($url){ Send-Scalar "appControl" "{`"method`":`"setActiveApp`",`"id`":1,`"version`":`"1.0`",`"params`":[{`"uri`":`"localapp://webappruntime?url=$url`"}]}" }

Write-Host "TV functions loaded. Target IP: $($global:TV_IP)" -ForegroundColor Green
Write-Host "Examples: PowerOff | VolUp | SwitchInput | HDMI1 | TvStatus" -ForegroundColor Green
