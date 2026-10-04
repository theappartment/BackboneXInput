# Verifiche della consegna

Data: 4 ottobre 2026. Host di verifica: macOS ARM64. SDK ufficiale Microsoft .NET 8.0.425 installato temporaneamente per il lavoro.

## Eseguite con successo

- Restore della soluzione e di tutte le dipendenze NuGet.
- Build Release della soluzione: **0 errori, 0 avvisi** (warning trattati come errori).
- **51 test automatici passati**: i 41 controlli precedenti e 10 dedicati ai comandi saltati: profilo completo senza Menu/View, neutralita dei pulsanti saltati, trigger/assi saltati, buchi non dichiarati, conflitto associato/saltato, nome sconosciuto, profilo tutto saltato, roundtrip JSON, compatibilita dei vecchi profili e riattivazione dopo nuova associazione.
- Publish autonomo **win-x64** della console e della nuova app Tray, con runtime .NET/Windows Forms e dipendenze incluse, riuscito.
- Ispezione `BackboneXInput.exe`: eseguibile **PE32+ console x86-64 per Windows**.
- Ispezione `BackboneXInput.Tray.exe`: eseguibile **PE32+ GUI x86-64 per Windows**, senza finestra console all'avvio.
- Smoke test della variante temporanea AnyCPU: `help` e `init --config ...` riusciti; `devices` su macOS rifiutato correttamente con errore Windows richiesto e codice uscita 1. La variante AnyCPU di verifica non e il pacchetto consegnato.

Il primo tentativo di build con compilazione condivisa si e bloccato nell'ambiente isolato ed e stato annullato. La build senza compilazione condivisa e passata. Un primo errore di API e stato corretto: il wrapper Vortice per GetDeviceState non e pubblico e il getter di alto livello non controlla l'HRESULT. Il sorgente usa la chiamata COM corrispondente e controlla l'errore prima di leggere lo stato.

## Da verificare su Windows con il Backbone fisico

- Enumerazione, apertura DirectInput e valori restituiti dal driver del proprio Backbone.
- Mapping e calibrazione fisici, soprattutto assi del secondo stick e LT/RT simultanei.
- Creazione Xbox 360 su ViGEmBus e riconoscimento in joy.cpl/XInput/gioco.
- Neutralizzazione dopo scollegamento e successiva riconnessione.
- Frequenza effettiva e latenza hardware sul PC.
- HidHide opzionale, whitelist e assenza di doppio input nel gioco.
- Menu START.cmd e script PowerShell su Windows. Il workflow Windows e stato preparato localmente ma non pubblicato: l'accesso GitHub usato non ha il permesso workflow. Non e stato eseguito da questo ambiente.
- Nuova icona/menu Windows Forms, toggle avvio Windows, avvio al login, sospensione e ripresa durante wizard/monitor, e whitelist HidHide di entrambi gli eseguibili. Non sono verificabili su macOS.
- Finestra grafica della v0.2.0, disegno del controller, layout con scaling DPI, conto alla rovescia, conferma/indietro/riprova, riepilogo e annullamento. La logica e testata; rendering e interazione WinForms non sono stati eseguiti su questo host macOS.
- Pulsante Salta della v0.3.0, progressione e riepilogo dei comandi disattivati, modifica di un passo saltato. La configurazione e la conversione sono testate; l'interazione WinForms resta da provare su Windows.

La consegna e compilabile e pubblicata per Windows; non viene dichiarata una prova hardware o del driver che macOS non puo eseguire. ViGEmBus/client sono ufficialmente EOL, come documentato nel README.
