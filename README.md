# Controle de TV Sony BRAVIA por rede (IRCC-IP + Scalar API)

Controle uma TV **Sony BRAVIA da geração 2010–2013** (sistema Linux/Opera,
**não** é Android TV) pela rede local: ligar, desligar, volume, trocar
entrada, navegar e até abrir uma página no navegador da TV — tudo por HTTP,
sem app, sem sideload, sem root.

Validado numa **KDL-42W805A** (2013, firmware PKG4.612BRA). O mesmo método
funciona em outras Bravia da mesma linha que exponham o serviço IRCC-IP.

## Por que isso funciona (e o que não dá)

Essas TVs usam firmware baseado em AOSP (Android Open Source Project), mas
**não são Android TV**: não têm Google Play, não instalam APK, não têm ADB
habilitável. O que elas expõem na rede é o **IRCC-IP** (controle remoto por
IP, protocolo SOAP) e a **Scalar Web API** (JSON, controle fino de estado).

Resumo honesto do que cada caminho entrega:

| Objetivo | Dá? | Observação |
|---|---|---|
| Controlar por rede (power, volume, entrada, setas) | ✅ | IRCC-IP, porta 80 |
| Ler/setar estado com valor exato (volume=10, entrada atual) | ✅ | Scalar Web API |
| Abrir uma URL no navegador da TV | ✅ | `appControl/setActiveApp` |
| Transmitir mídia do PC/celular | ✅ | DLNA/DMR, porta 52323 |
| Instalar app novo (APK/sideload) | ❌ | Não é Android TV |
| ADB por rede | ❌ | Porta 5555 fechada, sem developer options |
| Root | ❌ | Só via UART (hardware), risco de brick, sem ganho |

## ⚠️ Aviso de segurança

Nessa geração, o IRCC-IP costuma aceitar comandos **sem senha e sem
registro**: qualquer dispositivo na mesma rede controla a TV. Em casa, risco
baixo. Em recepção ou ambiente comercial, **separe o Wi-Fi de visitantes da
rede dos equipamentos.**

## Pré-requisitos

- A TV e o computador na **mesma rede local**.
- Na TV: `Menu → Configurações → Rede → Configuração de Rede Doméstica →
  Dispositivo Remoto / Renderer` ligado (`Sim`). "Início remoto" ligado
  habilita também o Wake-on-LAN.
- Windows com PowerShell (o script é `.ps1`). O método HTTP funciona de
  qualquer linguagem; o PowerShell é só a embalagem.

## Uso rápido (Windows / PowerShell)

1. Descubra o IP da TV: `Menu → Rede → Ver status da rede`.
2. Edite `controle-tv-sony.ps1` e troque `$global:TV_IP` pelo IP da sua TV.
3. Carregue as funções na sessão (o ponto-espaço no começo importa):
   ```powershell
   . .\controle-tv-sony.ps1
   ```
4. Comande por nome:
   ```powershell
   Liga
   Desliga
   VolSet 10
   MudaEntrada
   HDMI1
   TvStatus
   AbrePagina "https://exemplo.com"
   ```

## Comandos disponíveis

- **Energia:** `Liga`, `Desliga`, `PowerToggle`
- **Som:** `VolMais`, `VolMenos`, `Mudo`, `VolSet <n>`, `VolLer`
- **Entradas:** `MudaEntrada`, `HDMI1`..`HDMI4`, `ListaEntradas`
- **Navegação:** `Cima`, `Baixo`, `Esquerda`, `Direita`, `Ok`, `Voltar`,
  `Home`, `Opcoes`, `Display`
- **Canais:** `CanalMais`, `CanalMenos`
- **Estado:** `TvStatus`, `TvInfo`
- **Web:** `AbrePagina "<url>"`

> Power ON/OFF e volume foram confirmados na KDL-42W805A. Os códigos de
> navegação são os padrão Bravia; se algum não reagir no seu modelo, os
> códigos oficiais vêm no `dmr.xml` da própria TV (ver abaixo).

## Como controlar de qualquer linguagem (não só PowerShell)

### IRCC-IP (controle remoto) — `POST http://<IP>/sony/IRCC`

Corpo SOAP, header `SOAPAction: "urn:schemas-sony-com:service:IRCC:1#X_SendIRCC"`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
<s:Body><u:X_SendIRCC xmlns:u="urn:schemas-sony-com:service:IRCC:1">
<IRCCCode>AAAAAQAAAAEAAAASAw==</IRCCCode>
</u:X_SendIRCC></s:Body></s:Envelope>
```

### Scalar Web API — `POST http://<IP>/sony/<serviço>`

Corpo JSON-RPC. Exemplo, setar volume em 10:

```json
{"method":"setAudioVolume","id":1,"version":"1.0","params":[{"target":"speaker","volume":"10"}]}
```

Serviços expostos por essa geração: `guide`, `system`, `videoScreen`,
`audio`, `avContent`, `appControl`, `browser`, `notification`, `cec`,
`accessControl`, `irCommandProxy`.

## Descobrir os códigos e serviços da SUA TV

A própria TV publica a lista de serviços e alguns códigos IRCC em:

```
http://<IP>:52323/dmr.xml
```

Lá estão a `X_IRCCCodeList` (com Power/Power ON/Power OFF), a base da Scalar
API (`X_ScalarWebAPI_BaseURL`) e a lista de serviços. Outros endpoints úteis:

- IRCC: `http://<IP>/sony/IRCC`
- Lista de apps (DIAL): `http://<IP>/DIAL/sony/applist`

## Varredura de portas (referência, KDL-42W805A)

| Porta | Estado | Serviço |
|------:|--------|---------|
| 80 | aberta | HTTP: IRCC + Scalar API + DIAL |
| 52323 | aberta | DLNA/UPnP MediaRenderer |
| 443, 8008, 8009, 7676, 50001, 50002, 2869, 23, 5555 | fechadas | — |

## Licença

MIT. Veja [LICENSE](LICENSE). Use por sua conta e risco; mexa só no que é seu.
