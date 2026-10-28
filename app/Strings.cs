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

            ["TabRemote"] = "Remote",
            ["TabNumbers"] = "Numbers & colors",
            ["TabMedia"] = "Media",
            ["TabInputs"] = "Inputs",
            ["TabMore"] = "More",
            ["Enter"] = "Enter",
            ["Red"] = "Red",
            ["Green"] = "Green",
            ["Yellow"] = "Yellow",
            ["Blue"] = "Blue",
            ["Play"] = "Play",
            ["Pause"] = "Pause",
            ["Stop"] = "Stop",
            ["Rewind"] = "Rewind",
            ["Forward"] = "Forward",
            ["Prev"] = "Prev",
            ["Next"] = "Next",
            ["Guide"] = "Guide",
            ["Subtitle"] = "Subtitle",
            ["Audio"] = "Audio",
            ["Wide"] = "Wide",
            ["Exit"] = "Exit",
            ["PicOff"] = "Picture off",
            ["SyncMenu"] = "Sync menu",
            ["Refresh"] = "Refresh",
            ["InputsEmpty"] = "No inputs found. Click Refresh.",
            ["InputSwitched"] = "Switched to {0}.",
            ["InputConnected"] = "connected",
            ["VolumeExact"] = "Exact volume:",
            ["VolumeSet"] = "Volume set to {0}.",
            ["TextHint"] = "Text to type on the TV",
            ["SendText"] = "Send text",
            ["TextFieldOff"] = "Open a text field on the TV first",
            ["TextSent"] = "Text sent to the TV.",
            ["SyncClock"] = "Sync TV clock",
            ["ClockSet"] = "TV clock set.",
            ["ClockSetRead"] = "TV clock set. The TV now reads {0}.",
            ["NotSupported"] = "Not supported by this TV",
            ["Ant"] = "ANT",
            ["AntTip"] = "ANT: switch Antenna / Cable",
            ["TipReturn"] = "RETURN",
            ["TipHome"] = "HOME",
            ["TipOptions"] = "OPTIONS",
            ["TipDisplay"] = "DISPLAY",
            ["AllKeysLabel"] = "All TV keys:",
            ["ChooseKey"] = "Choose a key",
            ["SendKey"] = "Send",

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
@"1.3.0 — 2026-10-28

Added
• A fixed bar with Back, Home, Options and Display under the tabs, visible on every tab (tooltips: RETURN, HOME, OPTIONS, DISPLAY).
• ANT button on the Remote tab (HDMI3 | HDMI4 | ANT): switches the tuner between Antenna and Cable.
• ""All TV keys"" list on the More tab: every key the TV publishes, sorted, with a Send button.
• Enter in the URL field opens the page; Enter in the text field sends the text.
• The Inputs tab refreshes itself every 3 seconds while it is open, so the green dot follows the devices turning on and off.
• The volume bar follows the remote control: while the More tab is open it is re-read every 2 seconds.
• The volume now changes while you drag the bar or use the arrow, PageUp, PageDown, Home and End keys (at most one send every 250 ms; the last value is always sent).

Changed
• Tabs are easier to see: 14 pt text, a border on every tab and a highlighted, bold selected tab.
• Number pad: the last row is now Enter | 0 | OK. Enter is sent as before; OK sends Confirm, which acts at once.
• The Back, Home, Options and Display row moved from the Remote tab to the fixed bar.
• The window is 640 px tall and fits a 1280×800 screen.

Fixed
• The connected dot in the Inputs list no longer shifts the name to the left: every row uses an indicator of the same width.
• ""Analog"" is the ANT key (Antenna / Cable) on Brazilian TVs, not only a Japanese key.

Not included on purpose: the keys BS, CS, BSCS, Ddata, acTVila, RakurakuStart, Netflix, InternetVideo, InternetWidgets and DeleteVideo are left out of the keys list.

1.2.0 — 2026-10-28

Added
• Tabs: Remote (everything that was already there), Numbers & colors, Media, Inputs and More. Language, About, the TV IP and the status line stay visible on every tab.
• Number keys 0-9 and Enter, and the Red, Green, Yellow and Blue keys, in the order the TV reports (system/getColorKeysLayout; default RGYB).
• Media keys: Play, Pause, Stop, Rewind, Forward, Prev, Next.
• Inputs list built from the TV (avContent/getCurrentExternalInputsStatus) with its own titles and a ""connected"" sign, and a Refresh button. Click an input to switch to it (needs pairing). Unpaired, HDMI1-HDMI4 use the IRCC codes and the other inputs show ""Not paired"".
• Exact volume: a slider from 0 to the TV's maximum with the current number beside it. The volume is read when the More tab opens and after Vol -, Vol + and Mute, and is set (needs pairing) only when you release the slider. Unpaired, the slider is read-only.
• Type text on the TV (needs pairing): asks the TV first and says ""Open a text field on the TV first"" when none is open.
• Sync TV clock (needs pairing): sets the TV clock from the PC clock.
• Extra keys on More: Guide, Subtitle, Audio, Wide, Exit, Picture off, Sync menu.
• A TV that answers error 12, 14 or 15 (or has no such method) gets ""Not supported by this TV"" instead of a generic error.

Not included on purpose: deleteContent, setDeleteProtection, setLanguage, setPostalCode, setWolMode, setPowerSavingMode (they delete recordings or change TV settings; setWolMode could turn off the Wake-on-LAN that powers this TV on), the Japanese keys (BS, CS, Ddata, acTVila, Analog, Digital) and the Netflix, InternetVideo and InternetWidgets keys (services shut down).

1.1.1 — 2026-10-28

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

            ["TabRemote"] = "Controle",
            ["TabNumbers"] = "Números e cores",
            ["TabMedia"] = "Mídia",
            ["TabInputs"] = "Entradas",
            ["TabMore"] = "Mais",
            ["Enter"] = "Enter",
            ["Red"] = "Vermelho",
            ["Green"] = "Verde",
            ["Yellow"] = "Amarelo",
            ["Blue"] = "Azul",
            ["Play"] = "Play",
            ["Pause"] = "Pausar",
            ["Stop"] = "Parar",
            ["Rewind"] = "Retroceder",
            ["Forward"] = "Avançar",
            ["Prev"] = "Anterior",
            ["Next"] = "Próximo",
            ["Guide"] = "Guia",
            ["Subtitle"] = "Legenda",
            ["Audio"] = "Áudio",
            ["Wide"] = "Wide",
            ["Exit"] = "Sair",
            ["PicOff"] = "Apagar imagem",
            ["SyncMenu"] = "Menu Sync",
            ["Refresh"] = "Atualizar",
            ["InputsEmpty"] = "Nenhuma entrada encontrada. Clique em Atualizar.",
            ["InputSwitched"] = "Mudou para {0}.",
            ["InputConnected"] = "conectada",
            ["VolumeExact"] = "Volume exato:",
            ["VolumeSet"] = "Volume ajustado para {0}.",
            ["TextHint"] = "Texto para digitar na TV",
            ["SendText"] = "Enviar texto",
            ["TextFieldOff"] = "Abra um campo de texto na TV primeiro",
            ["TextSent"] = "Texto enviado para a TV.",
            ["SyncClock"] = "Acertar relógio da TV",
            ["ClockSet"] = "Relógio da TV acertado.",
            ["ClockSetRead"] = "Relógio da TV acertado. A TV agora marca {0}.",
            ["NotSupported"] = "Não suportado por esta TV",
            ["Ant"] = "ANT",
            ["AntTip"] = "ANT: alterna Antena / Cabo",
            ["TipReturn"] = "RETURN",
            ["TipHome"] = "HOME",
            ["TipOptions"] = "OPTIONS",
            ["TipDisplay"] = "DISPLAY",
            ["AllKeysLabel"] = "Todas as teclas da TV:",
            ["ChooseKey"] = "Escolha uma tecla",
            ["SendKey"] = "Enviar",

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
@"1.3.0 — 28/10/2026

Novo
• Barra fixa com Voltar, Início, Opções e Display abaixo das abas, visível em todas as abas (dicas: RETURN, HOME, OPTIONS, DISPLAY).
• Botão ANT na aba Controle (HDMI3 | HDMI4 | ANT): alterna o sintonizador entre Antena e Cabo.
• Lista ""Todas as teclas da TV"" na aba Mais: todas as teclas que a TV informa, em ordem alfabética, com botão Enviar.
• Enter no campo de endereço abre a página; Enter no campo de texto envia o texto.
• A aba Entradas se atualiza sozinha a cada 3 segundos enquanto está aberta, e a bolinha verde acompanha os aparelhos ligando e desligando.
• A barra de volume acompanha o controle remoto: com a aba Mais aberta, ela é lida de novo a cada 2 segundos.
• O volume agora muda enquanto você arrasta a barra ou usa as teclas Seta, PageUp, PageDown, Home e End (no máximo 1 envio a cada 250 ms; o último valor sempre é enviado).

Mudou
• As abas ficaram mais visíveis: texto de 14, borda em cada aba e aba selecionada em destaque e em negrito.
• Teclado numérico: a última linha agora é Enter | 0 | OK. O Enter é enviado como antes; o OK envia Confirm, que age na hora.
• A linha Voltar, Início, Opções e Display saiu da aba Controle e foi para a barra fixa.
• A janela tem 640 px de altura e cabe numa tela de 1280×800.

Corrigido
• A bolinha de entrada conectada não empurra mais o nome para a esquerda: todas as linhas usam um indicador do mesmo tamanho.
• ""Analog"" é a tecla ANT (Antena / Cabo) nas TVs brasileiras, não só uma tecla japonesa.

Não incluído de propósito: as teclas BS, CS, BSCS, Ddata, acTVila, RakurakuStart, Netflix, InternetVideo, InternetWidgets e DeleteVideo ficam fora da lista de teclas.

1.2.0 — 28/10/2026

Novo
• Abas: Controle (tudo o que já existia), Números e cores, Mídia, Entradas e Mais. Idioma, Sobre, o IP da TV e a linha de status ficam sempre visíveis.
• Teclado numérico 0-9 e Enter, e as teclas Vermelho, Verde, Amarelo e Azul, na ordem que a TV informa (system/getColorKeysLayout; padrão RGYB).
• Teclas de mídia: Play, Pausar, Parar, Retroceder, Avançar, Anterior, Próximo.
• Lista de entradas montada com os dados da TV (avContent/getCurrentExternalInputsStatus), com o título que ela informa e um sinal de ""conectada"", e botão Atualizar. Clique numa entrada para mudar para ela (exige pareamento). Sem pareamento, HDMI1-HDMI4 usam os códigos IRCC e as outras entradas mostram ""Não pareado"".
• Volume exato: barra de 0 até o máximo da TV, com o número atual ao lado. O volume é lido ao abrir a aba Mais e depois de Vol -, Vol + e Mudo, e só é mudado (exige pareamento) quando você solta a barra. Sem pareamento, a barra é só de leitura.
• Digitar texto na TV (exige pareamento): pergunta à TV antes e avisa ""Abra um campo de texto na TV primeiro"" quando não há campo aberto.
• Acertar relógio da TV (exige pareamento): acerta o relógio da TV com a hora do PC.
• Teclas extras em Mais: Guia, Legenda, Áudio, Wide, Sair, Apagar imagem, Menu Sync.
• Quando a TV responde erro 12, 14 ou 15 (ou não tem o método), aparece ""Não suportado por esta TV"" em vez de um erro genérico.

Não incluído de propósito: deleteContent, setDeleteProtection, setLanguage, setPostalCode, setWolMode, setPowerSavingMode (apagam gravações ou mudam configurações da TV; o setWolMode poderia desligar o Wake-on-LAN, que é o que liga esta TV), as teclas japonesas (BS, CS, Ddata, acTVila, Analog, Digital) e as teclas Netflix, InternetVideo e InternetWidgets (serviços desligados).

1.1.1 — 28/10/2026

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
