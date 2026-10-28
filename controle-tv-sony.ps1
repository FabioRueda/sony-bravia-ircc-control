<#
=====================================================================
 Controle da TV Sony BRAVIA (geracao 2010-2013) por rede
 IRCC-IP (controle remoto) + Scalar Web API (estado fino)
 Sem app, sem sideload, sem root.

 AVISO DE SEGURANCA: nessa geracao o IRCC-IP costuma aceitar comandos
 sem senha. Qualquer maquina na mesma rede controla a TV. Em ambiente
 comercial, separe o Wi-Fi de visitantes da rede dos equipamentos.

 Uso:  . .\controle-tv-sony.ps1     (carrega as funcoes na sessao)
       Liga ; Desliga ; VolSet 10 ; MudaEntrada ; TvStatus ; AbrePagina "https://exemplo.com"
=====================================================================
#>

$global:TV_IP = "192.168.1.100"   # <-- TROQUE pelo IP da sua TV (Menu > Rede > Ver status da rede)

# ---------- MOTOR: envia um codigo de controle remoto (IRCC) ----------
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
    Write-Host "Falhou (TV desligada ou fora da rede?): $($_.Exception.Message)" -ForegroundColor Yellow
    return $false
  }
}

# ---------- MOTOR: chama a Scalar Web API (estado e controle fino) ----------
function Send-Scalar($service,$json){
  try {
    Invoke-RestMethod -Uri "http://$($global:TV_IP)/sony/$service" -Method POST -Body $json -ContentType "application/json" -TimeoutSec 5
  } catch {
    Write-Host "Falhou na Scalar API: $($_.Exception.Message)" -ForegroundColor Yellow
  }
}

# =====================================================================
#  COMANDOS NOMEADOS (IRCC)
#  Power ON/OFF e volume confirmados na KDL-42W805A. Os demais sao os
#  codigos padrao Bravia; se algum nao reagir, confira o dmr.xml da TV
#  em http://<IP>:52323/dmr.xml
# =====================================================================
function Liga        { Send-IRCC "AAAAAQAAAAEAAAAuAw==" }   # Power ON
function Desliga     { Send-IRCC "AAAAAQAAAAEAAAAvAw==" }   # Power OFF
function PowerToggle { Send-IRCC "AAAAAQAAAAEAAAAVAw==" }   # liga/desliga alterna
function VolMais     { Send-IRCC "AAAAAQAAAAEAAAASAw==" }
function VolMenos    { Send-IRCC "AAAAAQAAAAEAAAATAw==" }
function Mudo        { Send-IRCC "AAAAAQAAAAEAAAAUAw==" }
function MudaEntrada { Send-IRCC "AAAAAQAAAAEAAAAlAw==" }   # cicla entradas
function CanalMais   { Send-IRCC "AAAAAQAAAAEAAAAQAw==" }
function CanalMenos  { Send-IRCC "AAAAAQAAAAEAAAARAw==" }

# Navegacao (setas, OK, voltar, menu)
function Cima        { Send-IRCC "AAAAAQAAAAEAAAB0Aw==" }
function Baixo       { Send-IRCC "AAAAAQAAAAEAAAB1Aw==" }
function Esquerda    { Send-IRCC "AAAAAQAAAAEAAAA0Aw==" }
function Direita     { Send-IRCC "AAAAAQAAAAEAAAAzAw==" }
function Ok          { Send-IRCC "AAAAAQAAAAEAAABlAw==" }
function Voltar      { Send-IRCC "AAAAAQAAAAEAAABjAw==" }
function Home        { Send-IRCC "AAAAAQAAAAEAAABgAw==" }
function Opcoes      { Send-IRCC "AAAAAQAAAAEAAAA2Aw==" }
function Display     { Send-IRCC "AAAAAQAAAAEAAABKAw==" }

# Entradas HDMI diretas
function HDMI1 { Send-IRCC "AAAAAgAAABoAAABaAw==" }
function HDMI2 { Send-IRCC "AAAAAgAAABoAAABbAw==" }
function HDMI3 { Send-IRCC "AAAAAgAAABoAAABcAw==" }
function HDMI4 { Send-IRCC "AAAAAgAAABoAAABdAw==" }

# =====================================================================
#  CONTROLE FINO (Scalar API) - le e escreve estado com numero exato
# =====================================================================

# Mostra o que esta na tela agora (entrada/app)
function TvStatus { Send-Scalar "avContent" '{"method":"getPlayingContentInfo","id":1,"version":"1.0","params":[]}' }

# Mostra info do sistema da TV
function TvInfo   { Send-Scalar "system" '{"method":"getSystemInformation","id":1,"version":"1.0","params":[]}' }

# Le o volume atual
function VolLer   { Send-Scalar "audio" '{"method":"getVolumeInformation","id":1,"version":"1.0","params":[]}' }

# Trava o volume num valor exato:  VolSet 10
function VolSet($n){ Send-Scalar "audio" "{`"method`":`"setAudioVolume`",`"id`":1,`"version`":`"1.0`",`"params`":[{`"target`":`"speaker`",`"volume`":`"$n`"}]}" }

# Lista as entradas disponiveis da TV
function ListaEntradas { Send-Scalar "avContent" '{"method":"getCurrentExternalInputsStatus","id":1,"version":"1.0","params":[]}' }

# Abre uma pagina web no navegador da TV
function AbrePagina($url){ Send-Scalar "appControl" "{`"method`":`"setActiveApp`",`"id`":1,`"version`":`"1.0`",`"params`":[{`"uri`":`"localapp://webappruntime?url=$url`"}]}" }

Write-Host "Funcoes da TV carregadas. IP alvo: $($global:TV_IP)" -ForegroundColor Green
Write-Host "Exemplos: Liga | Desliga | VolSet 10 | MudaEntrada | TvStatus | AbrePagina 'https://exemplo.com'" -ForegroundColor Green
