# BackboneXInput

[Scarica il pacchetto Windows x64](https://github.com/theappartment/BackboneXInput/releases/tag/v0.1.0) oppure consulta [tutte le release](https://github.com/theappartment/BackboneXInput/releases). La prima release e preliminare: build e test automatici verificati, prova con hardware/driver Windows ancora necessaria.

App Windows C#/.NET con icona vicino all'orologio e strumenti console: legge il controller gia riconosciuto da `joy.cpl` con DirectInput e trasmette i comandi a un Xbox 360 virtuale tramite ViGEmBus. Non installa driver, non modifica HidHide e non nasconde dispositivi. Nessun VID/PID o mapping Backbone e preimpostato.

## Scelta delle librerie, verificata il 4 ottobre 2026

- **Vortice.DirectInput 3.8.3**: binding DirectInput aggiornato nel 2026, compatibile con .NET 8. Scelto per leggere pulsanti, tutti gli otto slot assi standard e quattro POV senza conoscere il report HID proprietario. [NuGet ufficiale](https://www.nuget.org/packages/Vortice.DirectInput/3.8.3), [sorgenti](https://github.com/amerkoleci/Vortice.Windows).
- **Nefarius.ViGEm.Client 1.21.256 + ViGEmBus**: backend pubblico gratuito disponibile per emulare Xbox 360/XInput. **Entrambi sono ritirati e non ricevono aggiornamenti**: non sono presentati come mantenuti. La scelta e pratica per questo progetto personale, non una garanzia di compatibilita futura. [Client](https://www.nuget.org/packages/Nefarius.ViGEm.Client/1.21.256), [dichiarazione EOL](https://docs.nefarius.at/projects/ViGEm/End-of-Life/).
- Il successore **VirtualPad** e un framework commerciale per partner aziendali; non e un SDK pubblico gratuito sostituibile senza accordi. [Documentazione ufficiale](https://docs.nefarius.at/projects/VirtualPad/).
- **HidHide** e una soluzione opzionale per il doppio input. Installazione e attivazione sono esclusivamente manuali. [Progetto ufficiale](https://github.com/nefarius/HidHide), [guida](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/).

Questa verifica distingue una libreria input aggiornata da un backend virtuale legacy: non e emersa una sostituzione pubblica gratuita di pari disponibilita nelle fonti ufficiali consultate. Il backend e isolato in `VirtualXbox.cs` per consentire una futura sostituzione.

## Prerequisiti

1. Windows 10/11 **x64 Intel/AMD**. Questo pacchetto non supporta ARM64 o x86.
2. Backbone One visibile e funzionante in `joy.cpl`, con lo stesso collegamento attuale. Non serve un driver USB personalizzato.
3. Per `run` e `diagnose`, installa manualmente [ViGEmBus Setup 1.22.0 ufficiale](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0), autorizzando l'installer e riavviando se richiesto. L'app normalmente non richiede privilegi amministrativi. Il setup 1.22.0 rimuove l'updater legacy; per vecchie installazioni segui la [guida EOL](https://docs.nefarius.at/projects/ViGEm/End-of-Life/), evitando il vecchio dominio vigem.org.
4. Per compilare: SDK .NET 8.0 aggiornato dal [sito Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0). Il pacchetto `windows-x64` include il runtime, quindi non richiede .NET installato per l'uso. .NET 8 si avvicina al termine del supporto: pianificare l'aggiornamento del target per un uso prolungato.

I driver non sono inclusi nel pacchetto e non vengono scaricati o installati dall'app.

## Prova rapida con il pacchetto Windows

### Avvio automatico e collegamento del controller

1. Estrai il pacchetto completo in una cartella stabile e installa manualmente ViGEmBus come indicato sopra.
2. Apri `AVVIA-AUTOMATICO.cmd` oppure `windows-x64\BackboneXInput.Tray.exe`. Compare un'icona nella zona dell'orologio; Windows potrebbe mostrarla nel menu delle icone nascoste.
3. Con il tasto destro sull'icona apri **Mapping Wizard** e completa il mapping una volta. Seleziona prima il controller dal menu se necessario. Gli strumenti si aprono in una console: premi Invio alla fine per chiuderla. Il bridge riprende automaticamente e rilegge il file aggiornato.
4. Nel menu attiva **Avvia con Windows**. L'opzione e inizialmente disattivata: registra soltanto l'avvio di questa app per l'utente corrente, senza amministratore. Non installa un servizio.
5. Ai successivi accessi a Windows l'app parte senza console e resta in attesa. Quando colleghi il Backbone, il controllo ogni secondo lo rileva e crea l'Xbox virtuale al primo stato valido. Quando lo scolleghi, invia lo stato neutro, rimuove l'Xbox virtuale e torna in attesa. Non occorre aprire `joy.cpl`.

L'avvio e al **login dell'utente**, non prima del login o sul solo evento USB con app chiusa. Windows puo ritardare le app di avvio. Se non parte, controlla Impostazioni > App > Avvio. Usa **Esci** dal menu per fermarla fino al prossimo accesso; disattiva **Avvia con Windows** per eliminare l'avvio automatico. Se sposti la cartella, riattiva l'opzione dal nuovo eseguibile. Una mappatura mancante/incompleta o un driver non disponibile appare come stato di errore nel menu; l'app riprova ogni cinque secondi e registra il dettaglio nei log.

Il menu include monitor, selezione, wizard, diagnostica e apertura log. Durante uno strumento console, il bridge automatico e sospeso fino alla chiusura della console per consentire configurazione e lettura senza conflitti. Non lanciare contemporaneamente `run` dal vecchio menu: il mutex blocca il secondo bridge.

L'icona usa [NotifyIcon di Windows Forms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms). L'opzione di avvio usa il valore `BackboneXInput` sotto `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, tra i [meccanismi di avvio documentati da Microsoft](https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows).

### Strumenti manuali

Estrai tutto lo ZIP. `START.cmd` apre un menu per elenco, selezione, monitor, wizard, diagnostica e avvio. In alternativa apri PowerShell nella cartella `windows-x64` ed esegui:

```powershell
.\BackboneXInput.exe devices
.\BackboneXInput.exe monitor
.\BackboneXInput.exe wizard
.\BackboneXInput.exe diagnose
.\BackboneXInput.exe run
```

Ferma monitor/run con Ctrl+C. `wizard` non crea il controller virtuale e funziona anche senza ViGEmBus. Se piu controller corrispondono al nome, oppure il nome e differente, esegui `select` e scegli il **controller fisico**. La selezione salva il GUID realmente enumerato. Non scegliere l'Xbox virtuale di un altro remapper.

## Restore, build, test e avvio dai sorgenti

Dalla cartella con `BackboneXInput.sln`:

```powershell
dotnet restore BackboneXInput.sln
dotnet build BackboneXInput.sln -c Release --no-restore
dotnet run --project tests/BackboneXInput.Tests -c Release --no-build
dotnet run --project src/BackboneXInput -c Release -- devices
dotnet run --project src/BackboneXInput -c Release -- wizard
dotnet run --project src/BackboneXInput -c Release -- run
dotnet publish src/BackboneXInput -c Release -r win-x64 --self-contained true -o windows-x64
dotnet publish src/BackboneXInput.Tray -c Release -r win-x64 --self-contained true -o windows-x64
```

`build.ps1 -Publish` esegue restore/build/test/publish e interrompe su errore. I test sono un eseguibile autonomo senza framework esterno; **`dotnet test` non li esegue**, usare il comando sopra. Lo stesso script puo essere eseguito da un runner CI Windows.

## Monitor e mapping

`monitor` mostra ogni 100 ms tutti gli slot raw DirectInput, normalizzazione nominale -1..1, POV, pulsanti premuti e report Xbox normalizzato con calibrazione/deadzone. La lettura resta a 200 Hz; la visualizzazione non e un registro di ogni transizione brevissima. La numerazione pulsanti e `indice JSON / numero joy.cpl`: ad esempio `0/1` indica lo stesso pulsante. Gli indici JSON sono sempre da zero.

Raw significa **stato DirectInput**, non byte del report USB/HID. Gli assi vengono richiesti a DirectInput nel range 0..65535; deadzone e saturazione DirectInput vengono disattivate quando il driver lo consente. Queste proprieta appartengono alla sessione di lettura. I quattro slot POV possono comprendere slot assenti, riportati senza attivita dal dispositivo. La diagnostica enumera anche gli oggetti effettivamente presenti.

Il wizard esegue A/B/X/Y, LB/RB, LT/RT, L3/R3, quattro direzioni D-pad, Menu/View e i quattro assi stick. Per ciascuno:

1. Rilascia tutti i comandi, centra gli stick e premi Invio.
2. Premi solo il comando richiesto per la finestra di quattro secondi. Per trigger, premi a fondo. Per stick, tieni la direzione indicata fino alla richiesta della direzione opposta.
3. Verifica l'input rilevato e conferma con Invio, oppure `r` per ripetere.

Il wizard puo rilevare pulsanti, POV cardinali o assi. Per LT/RT preferisce un asse se presente, altrimenti salva un trigger digitale 0/255. Stick X positivo = destra; Y positivo = alto come Xbox. Range asimmetrici e inversione sono ricavati dalle estremita misurate. Il risultato e salvato solo al termine; il file precedente diventa `.bak`. Ctrl+C durante una cattura annulla. Durante una richiesta di testo, premi anche Invio per sbloccare la console dopo Ctrl+C. In caso di scollegamento durante il wizard, riconnetti e riavvialo; il vecchio file resta intatto.

Il rilevamento propone il segnale piu evidente; non puo sapere il significato fisico di un input. Premi un solo comando, controlla il JSON mostrato e conferma con attenzione. Dopo il wizard controlla nel monitor tutte le direzioni, i trigger e le diagonali. Se LT/RT condividono un asse, DirectInput potrebbe perderne la pressione simultanea: il software non puo ricostruire due valori indipendenti da uno solo. In quel caso occorre verificare il report HID reale prima di aggiungere un backend HID specifico.

## Configurazione JSON

Percorso predefinito: `%LOCALAPPDATA%\BackboneXInput\config.json`. `init` crea un file vuoto senza sovrascrivere. Ogni comando accetta `--config "C:\percorso\config.json"`. `config.unmapped.json` e un modello intenzionalmente privo di mapping, quindi `run` lo rifiuta finche il wizard non ha completato tutti i controlli.

| Campo | Significato |
| --- | --- |
| `DeviceNameContains` | Ricerca senza distinzione maiuscole, solo quando il GUID non e impostato |
| `DeviceInstanceGuid` | Identita enumerata realmente; usata in via esclusiva se presente |
| `PollingHz` | 125..250, default 200 |
| `LeftStickDeadzone`, `RightStickDeadzone` | Deadzone radiale 0..meno di 1; tipicamente 0.08..0.15 |
| `Mappings` | Chiavi: A/B/X/Y/LB/RB/LT/RT/L3/R3/Up/Down/Left/Right/Menu/View/LeftX/LeftY/RightX/RightY |
| `Kind` | `Button`, `Axis`, `Pov` |
| `Index` | Pulsante 0..127, asse 0..7, POV 0..3 |
| `Min`, `Center`, `Max`, `Invert` | Calibrazione stick e inversione; Min < Center < Max |
| `Rest`, `Full`, `Deadzone` | Asse usato come trigger o pulsante: riposo, pressione completa e deadzone; Full puo essere inferiore a Rest |
| `Threshold` | Soglia 0..1 escluso zero, per un asse assegnato a un pulsante |
| `PovAngle` | 0 alto, 9000 destra, 18000 basso, 27000 sinistra; diagonali attivano entrambe le direzioni vicine |

Assi standard: 0=X, 1=Y, 2=Z, 3=RotationX, 4=RotationY, 5=RotationZ, 6=Slider0, 7=Slider1. Sono nomi DirectInput, **non corrispondenze Backbone**. Non copiare un mapping di un dispositivo differente. Campi JSON sconosciuti, indici fuori range, valori non finiti, calibrazione degenerata e configurazioni incomplete vengono rifiutati. Modifica il JSON a programma fermo, poi riavvia: non c'e ricaricamento automatico.

## Verifica Windows, joy.cpl e giochi

1. Prima del bridge verifica il Backbone fisico in `joy.cpl`.
2. Avvia `run`, poi riapri `joy.cpl`: deve comparire anche il controller Xbox 360 virtuale. Il nome esatto puo dipendere da Windows. Testa pulsanti, quattro direzioni e trigger.
3. Prova un gioco con XInput. Avvia il bridge prima del gioco; chiudi altri remapper. XInput espone al massimo quattro controller, quindi libera eventuali slot occupati.
4. In Steam evita che Steam Input rimappi contemporaneamente il controller fisico e quello virtuale. Per la prova iniziale usa un gioco XInput con Steam Input disabilitato per quel gioco, oppure configura Steam affinche usi solo il controller virtuale. La scelta dipende dal gioco.
5. Scollega il Backbone mentre tieni premuto A: il report deve tornare neutro e l'Xbox virtuale deve scomparire. Ricollegalo: il bridge cerca il dispositivo ogni secondo e ricrea l'Xbox dopo il primo stato valido. Se Windows assegna un altro GUID, esegui nuovamente `select` e verifica/calibra il mapping. Alcuni giochi richiedono di essere riavviati quando un controller viene aggiunto/rimosso.
6. Ferma con Ctrl+C: il controller virtuale viene disconnesso. Un errore del backend virtuale termina il bridge con codice 1; uno scollegamento input avvia la riconnessione.

Il loop e sincrono su un solo thread, usa scadenze monotone, una richiesta temporanea di risoluzione timer Windows 1 ms e attese cancellabili. Non usa busy spinning. Ogni cinque secondi registra frequenza **effettiva** e cicli in ritardo: 200 Hz e un obiettivo di campionamento, non una promessa di latenza USB o scheduling. Il monitor puo essere piu lento a causa della console. Il mutex impedisce due `run` contemporanei nella stessa sessione Windows.

## Doppio input: HidHide, solo su tua scelta

Se il gioco vede sia Backbone sia Xbox virtuale e produce doppi comandi, puoi scegliere di installare HidHide. L'app non ne modifica mai configurazione o whitelist.

1. Scarica [HidHide dal repository ufficiale](https://github.com/nefarius/HidHide/releases), installalo e riavvia secondo le istruzioni.
2. Usa il pacchetto pubblicato, conservandolo in un percorso stabile. Nel Configuration Client, scheda Applications, autorizza **`BackboneXInput.Tray.exe` per il bridge automatico** e **`BackboneXInput.exe` per wizard/monitor/strumenti manuali**, entrambi nella cartella `windows-x64`. Se usi una precedente whitelist che include solo BackboneXInput.exe, devi aggiungere anche il nuovo eseguibile Tray.
3. Nella scheda Devices seleziona soltanto il Backbone fisico e attiva Enable device hiding. Non selezionare il controller Xbox virtuale. Mantieni inverse application cloak disattivato.
4. Ricollega il Backbone e riavvia il bridge. Verifica che il monitor continui a leggerlo e che `joy.cpl`/gioco vedano solo l'Xbox virtuale durante `run`.
5. Per annullare, disattiva Enable device hiding, ricollega e riapri il gioco. Se sposti l'eseguibile, aggiorna la whitelist.

Preferisci l'eseguibile pubblicato a `dotnet run` per HidHide: autorizzare genericamente dotnet.exe concederebbe l'accesso ad altre applicazioni .NET eseguite tramite lo stesso host. Non installare HidGuardian insieme a HidHide. Segui la [guida ufficiale](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/).

## Diagnostica ed errori

`diagnose` mostra sistema, configurazione, dispositivi, oggetti DirectInput, uno stato e prova a creare/inviare/disconnettere un Xbox neutro. Non prova la latenza hardware e non certifica il funzionamento nel gioco.

Log: `%LOCALAPPDATA%\BackboneXInput\logs\AAAA-MM-GG.log`, con eccezioni/HRESULT; oltre 5 MB il file giornaliero ruota su `.old`. Non si registrano tutti i pacchetti nel loop per evitare carico inutile. I file dei giorni precedenti restano disponibili e si possono eliminare manualmente.

| Problema | Controllo |
| --- | --- |
| Nessun Backbone | joy.cpl, cavo, `devices`, `select`, whitelist HidHide |
| Piu Backbone | `select` sceglie il GUID; nessuna selezione arbitraria |
| Errori range/apertura | `diagnose`, oggetti/driver DirectInput; questa versione richiede assi standard scalabili 0..65535 |
| Stick invertito | Ripeti wizard o correggi `Invert`; controlla soprattutto Y |
| Drift | Aumenta deadzone o ricalibra il centro |
| Trigger sempre premuto | Verifica Rest/Full/Invert e il valore raw a riposo |
| LT/RT simultanei errati | Verifica se condividono un asse; potrebbe servire HID specifico dopo analisi del descrittore |
| ViGEm non disponibile | Installa setup ufficiale, riavvia, verifica il bus in Gestione dispositivi; consulta la guida ufficiale per vecchi driver in conflitto |
| Frequenza bassa | Usa `run`, non monitor; riduci PollingHz a 125 e controlla il carico sistema |
| Comandi doppi | Ferma remapper concorrenti, verifica Steam Input, valuta HidHide manualmente |

Non e implementata la vibrazione verso il Backbone, perche non conosciamo il suo protocollo di feedback. Il monitor mostra i valori DirectInput disponibili; non interpreta report HID proprietari o inventa assi mancanti.

## Disinstallazione

1. Nel menu dell'icona disattiva **Avvia con Windows**, poi scegli **Esci**. Ferma eventuali strumenti console con Ctrl+C. Se hai gia cancellato la cartella, rimuovi soltanto il valore `BackboneXInput` da `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; non cancellare la chiave Run o altre voci.
2. Se hai scelto HidHide, disattiva il nascondimento del Backbone e rimuovi l'app dalla whitelist. Puoi disinstallare HidHide dalle app Windows e riavviare se non lo usano altri programmi.
3. Elimina la cartella dell'app. Per cancellare configurazione/log elimina `%LOCALAPPDATA%\BackboneXInput`.
4. Se nessun altro remapper usa ViGEmBus, disinstallalo dalle app Windows e riavvia. Per installazioni residue segui [Installazione/rimozione ufficiale](https://docs.nefarius.at/projects/ViGEm/How-to-Install/).

## Struttura e verifiche

`src/BackboneXInput.Core`: JSON, validazione, normalizzazione, conversione e ciclo di vita dell'output indipendenti da Windows. `src/BackboneXInput`: console, DirectInput, wizard, logging e ViGEm. `src/BackboneXInput.Tray`: icona, menu, avvio Windows e gestione del bridge in background. `tests`: controlli automatici del core. `windows-x64`: pubblicazione autonoma quando presente. Vedi `VERIFICATION.md` per i controlli eseguiti nella consegna e quelli ancora da effettuare sul PC.
