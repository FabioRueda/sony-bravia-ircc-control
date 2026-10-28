using System.Collections.Generic;
using System.Globalization;

// Every user-visible text, one dictionary per language. Nothing else in the app
// hard-codes UI text. Both dictionaries must have the same keys (see tests).

namespace SonyTvRemote
{
    public enum Lang { EnUs, PtBr }

    public static class Strings
    {
        private static readonly Dictionary<string, string> EnUs = new Dictionary<string, string>
        {
            ["AppTitle"] = "Sony BRAVIA Remote",
            ["TitleWithVersion"] = "{0} v{1}",
            ["LanguageLabel"] = "Language:",
            ["LangEnUs"] = "English (US)",
            ["LangPtBr"] = "Português (Brasil)",
            ["About"] = "About",
            ["TvIp"] = "TV IP:",
            ["Pair"] = "Pair with TV",
            ["PowerOn"] = "Power On",
            ["PowerOff"] = "Power Off",
            ["Mute"] = "Mute",
            ["VolDown"] = "Vol -",
            ["VolUp"] = "Vol +",
            ["ChDown"] = "Ch -",
            ["ChUp"] = "Ch +",
            ["Confirm"] = "OK",
            ["Back"] = "Back",
            ["Home"] = "Home",
            ["Options"] = "Options",
            ["Display"] = "Display",
            ["Input"] = "Input",
            ["Hdmi1"] = "HDMI1",
            ["Hdmi2"] = "HDMI2",
            ["Hdmi3"] = "HDMI3",
            ["Hdmi4"] = "HDMI4",
            ["OpenOnTv"] = "Open on TV",
            ["UrlHint"] = "URL",

            ["Ready"] = "Ready. Set the TV IP and click.",
            ["EnterIp"] = "Enter the TV IP.",
            ["InvalidIp"] = "Invalid TV IP. Example: 192.168.1.100",
            ["EnterUrl"] = "Enter a URL.",
            ["SentOk"] = "OK (HTTP {0})",
            ["HttpFailed"] = "Failed: HTTP {0}",
            ["TvError"] = "The TV answered with error {0}.",
            ["NoResponse"] = "No response (TV off or off-network?).",
            ["Unexpected"] = "Unexpected error. Try again.",
            ["Busy"] = "Please wait…",
            ["NotPaired"] = "Not paired — click Pair with TV",
            ["PageSent"] = "Page sent to the TV.",
            ["PairAsking"] = "Asking the TV for a PIN…",
            ["PairCancelled"] = "Pairing cancelled.",
            ["Paired"] = "Paired. Valid until {0:d}.",
            ["PairFailed"] = "Pairing failed: HTTP {0}",
            ["PinTitle"] = "Pair with TV",
            ["PinPrompt"] = "Type the 4-digit PIN shown on the TV:",
            ["Ok"] = "OK",
            ["Cancel"] = "Cancel",
            ["PowerWaking"] = "Turning the TV on…",
            ["PowerOnOk"] = "On",
            ["PowerStillStandby"] = "Still in standby",
            ["PowerNoMac"] = "Still in standby. No Wake-on-LAN MAC saved for this IP yet: use any button once with the TV on.",

            ["AboutTitle"] = "About",
            ["Version"] = "Version {0}",
            ["Repository"] = "Repository:",
            ["History"] = "Version history",
            ["Close"] = "Close",
            ["ChangelogText"] =
@"1.1.1 — 2026-10-28

Fixed
• Wake-on-LAN MAC and pairing are now stored per TV (by IP); switching the IP no longer wakes or sends the cookie to the previous TV.

Changed
• settings.json keeps the MAC and pairing under Tvs, one entry per IP. A 1.1.0 file is migrated automatically to the saved IP, keeping the current pairing.
• Power On with no MAC saved for the current IP asks that TV for it (system/getSystemSupportedFunction) before giving up.
• An unreadable settings.json is copied to settings.json.bad before the app falls back to defaults.

1.1.0 — 2026-10-28

Added
• Version number in the window title and an About window with the version history.
• Language selector: English (US) and Português (Brasil). The choice and the last TV IP are saved in %APPDATA%\SonyTvRemote\settings.json.
• Pair with TV: PIN pairing (accessControl/actRegister). The auth cookie is stored encrypted with Windows DPAPI and renewed automatically, without a PIN, when less than 2 days remain.
• Power On via Wake-on-LAN, using the MAC the TV reports (system/getSystemSupportedFunction).
• Display button.

Changed
• IRCC codes now come from the TV itself (system/getRemoteControllerInfo), with a built-in table of the official codes as a fallback.
• Power On: Wake-on-LAN first; if the TV is still in standby after 6 s, setPowerStatus (when paired) and IRCC Power ON; status checked for up to 20 s. On the KDL-42W805A only Wake-on-LAN turns the TV on: setPowerStatus answers error 15 ""power on not supported"" and IRCC Power ON is ignored in standby.
• Open on TV requires pairing and adds https:// when the URL has no scheme.
• PowerShell script: TvStatus uses system/getPowerStatus and TvInfo uses system/getInterfaceInformation (no pairing needed). OpenPage and VolSet are documented as requiring pairing.

Fixed
• Options sent the timer code; it now sends the official Options code.
• Back sent ""Exit""; it now sends ""Return"".
• Display code in the PowerShell script.
• Open on TV always failed with HTTP 403 because the TV requires pairing.

1.0.0 — 2026-10-28

Added
• Initial version, never published as a release: Windows GUI remote (Avalonia) with power, volume, D-pad, inputs, channels and ""open a URL on the TV"", plus the PowerShell script.",
        };

        private static readonly Dictionary<string, string> PtBr = new Dictionary<string, string>
        {
            ["AppTitle"] = "Sony BRAVIA Remote",
            ["TitleWithVersion"] = "{0} v{1}",
            ["LanguageLabel"] = "Idioma:",
            ["LangEnUs"] = "English (US)",
            ["LangPtBr"] = "Português (Brasil)",
            ["About"] = "Sobre",
            ["TvIp"] = "IP da TV:",
            ["Pair"] = "Parear com a TV",
            ["PowerOn"] = "Ligar",
            ["PowerOff"] = "Desligar",
            ["Mute"] = "Mudo",
            ["VolDown"] = "Vol -",
            ["VolUp"] = "Vol +",
            ["ChDown"] = "Canal -",
            ["ChUp"] = "Canal +",
            ["Confirm"] = "OK",
            ["Back"] = "Voltar",
            ["Home"] = "Início",
            ["Options"] = "Opções",
            ["Display"] = "Display",
            ["Input"] = "Entrada",
            ["Hdmi1"] = "HDMI1",
            ["Hdmi2"] = "HDMI2",
            ["Hdmi3"] = "HDMI3",
            ["Hdmi4"] = "HDMI4",
            ["OpenOnTv"] = "Abrir na TV",
            ["UrlHint"] = "Endereço (URL)",

            ["Ready"] = "Pronto. Confira o IP da TV e clique.",
            ["EnterIp"] = "Digite o IP da TV.",
            ["InvalidIp"] = "IP da TV inválido. Exemplo: 192.168.1.100",
            ["EnterUrl"] = "Digite um endereço (URL).",
            ["SentOk"] = "OK (HTTP {0})",
            ["HttpFailed"] = "Falhou: HTTP {0}",
            ["TvError"] = "A TV respondeu com o erro {0}.",
            ["NoResponse"] = "Sem resposta (TV desligada ou fora da rede?).",
            ["Unexpected"] = "Erro inesperado. Tente de novo.",
            ["Busy"] = "Aguarde…",
            ["NotPaired"] = "Não pareado — clique em Parear com a TV",
            ["PageSent"] = "Página enviada para a TV.",
            ["PairAsking"] = "Pedindo o PIN à TV…",
            ["PairCancelled"] = "Pareamento cancelado.",
            ["Paired"] = "Pareado. Válido até {0:d}.",
            ["PairFailed"] = "O pareamento falhou: HTTP {0}",
            ["PinTitle"] = "Parear com a TV",
            ["PinPrompt"] = "Digite o PIN de 4 dígitos que aparece na TV:",
            ["Ok"] = "OK",
            ["Cancel"] = "Cancelar",
            ["PowerWaking"] = "Ligando a TV…",
            ["PowerOnOk"] = "Ligada",
            ["PowerStillStandby"] = "Ainda em standby",
            ["PowerNoMac"] = "Ainda em standby. Ainda não há MAC do Wake-on-LAN salvo para este IP: use qualquer botão uma vez com a TV ligada.",

            ["AboutTitle"] = "Sobre",
            ["Version"] = "Versão {0}",
            ["Repository"] = "Repositório:",
            ["History"] = "Histórico de versões",
            ["Close"] = "Fechar",
            ["ChangelogText"] =
@"1.1.1 — 28/10/2026

Corrigido
• O MAC do Wake-on-LAN e o pareamento agora ficam guardados por TV (pelo IP); trocar o IP não liga mais a TV anterior nem manda o cookie para ela.

Mudou
• O settings.json guarda o MAC e o pareamento em Tvs, uma entrada por IP. Um arquivo da 1.1.0 é migrado sozinho para o IP salvo, sem perder o pareamento.
• Ligar sem MAC salvo para o IP atual pergunta o MAC a essa TV (system/getSystemSupportedFunction) antes de desistir.
• Um settings.json ilegível é copiado para settings.json.bad antes de o app voltar ao padrão.

1.1.0 — 28/10/2026

Novo
• Número da versão no título da janela e janela Sobre com o histórico de versões.
• Seletor de idioma: English (US) e Português (Brasil). A escolha e o último IP da TV ficam salvos em %APPDATA%\SonyTvRemote\settings.json.
• Parear com a TV: pareamento por PIN (accessControl/actRegister). O cookie de autenticação fica salvo criptografado com o DPAPI do Windows e é renovado sozinho, sem PIN, quando faltam menos de 2 dias para vencer.
• Ligar por Wake-on-LAN, com o MAC que a própria TV informa (system/getSystemSupportedFunction).
• Botão Display.

Mudou
• Os códigos IRCC agora vêm da própria TV (system/getRemoteControllerInfo), com uma tabela embutida dos códigos oficiais como reserva.
• Ligar: primeiro Wake-on-LAN; se a TV continuar em standby depois de 6 s, setPowerStatus (se pareado) e IRCC Power ON; o estado é consultado por até 20 s. Na KDL-42W805A só o Wake-on-LAN liga a TV: o setPowerStatus responde erro 15 ""power on not supported"" e o IRCC Power ON é ignorado em standby.
• Abrir na TV exige pareamento e acrescenta https:// quando o endereço vem sem protocolo.
• Script PowerShell: TvStatus usa system/getPowerStatus e TvInfo usa system/getInterfaceInformation (sem pareamento). Documentado que OpenPage e VolSet exigem pareamento.

Corrigido
• Opções mandava o código do timer; agora manda o código oficial de Options.
• Voltar mandava ""Exit""; agora manda ""Return"".
• Código do Display no script PowerShell.
• Abrir na TV sempre falhava com HTTP 403, porque a TV exige pareamento.

1.0.0 — 28/10/2026

Novo
• Versão inicial, nunca publicada como release: controle remoto com janela para Windows (Avalonia), com ligar/desligar, volume, setas, entradas, canais e ""abrir um endereço na TV"", mais o script PowerShell.",
        };

        public static Lang Current { get; set; } = Lang.EnUs;

        public static IReadOnlyDictionary<string, string> Table(Lang lang) => lang == Lang.PtBr ? PtBr : EnUs;

        public static CultureInfo Culture(Lang lang) => CultureInfo.GetCultureInfo(Code(lang));

        public static string Get(Lang lang, string key, params object[] args)
        {
            var text = Table(lang).TryGetValue(key, out var t) ? t : key;
            return args.Length == 0 ? text : string.Format(Culture(lang), text, args);
        }

        public static string Get(string key, params object[] args) => Get(Current, key, args);

        public static string Code(Lang lang) => lang == Lang.PtBr ? "pt-BR" : "en-US";

        public static Lang FromCode(string code) => code == "pt-BR" ? Lang.PtBr : Lang.EnUs;
    }
}
