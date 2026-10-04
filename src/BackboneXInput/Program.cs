using System.Diagnostics;
using System.Runtime.InteropServices;
using BackboneXInput.Core;

namespace BackboneXInput;

public static class Program
{
    private static int Main(string[] args)
    {
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        try
        {
            var command = args.FirstOrDefault() ?? "help";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackboneXInput", "config.json");
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--pause") continue;
                if (args[i] != "--config" || i + 1 >= args.Length) throw new ArgumentException("Opzione sconosciuta. Usa --config percorso.json.");
                path = Path.GetFullPath(args[++i]);
            }
            if (command == "help") { Help(); return 0; }
            if (command == "init")
            {
                if (File.Exists(path)) throw new IOException("Configurazione esistente: non sovrascritta.");
                ConfigStore.Save(path, new()); Console.WriteLine(path); return 0;
            }
            if (command is not ("devices" or "select" or "monitor" or "wizard" or "run" or "diagnose")) throw new ArgumentException("Comando sconosciuto.");
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DirectInput e ViGEm richiedono Windows 10/11 x64.");
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Usare il processo x64.");
            var config = File.Exists(path) ? ConfigStore.Load(path) : new AppConfig();
            using var source = new DirectInputSource();
            if (command == "devices") { ListDevices(source); return 0; }
            if (command == "select")
            {
                var devices = source.Enumerate();
                ListDevices(source);
                Console.WriteLine("Numero del controller fisico da usare (non Xbox virtuale):");
                if (!int.TryParse(Console.ReadLine(), out var selected) || selected < 1 || selected > devices.Count) throw new ArgumentException("Selezione non valida.");
                config.DeviceInstanceGuid = devices[selected - 1].InstanceGuid;
                ConfigStore.Save(path, config); Console.WriteLine("Selezione salvata."); return 0;
            }
            if (command == "diagnose")
            {
                Console.WriteLine($"Windows: {Environment.OSVersion}; .NET: {Environment.Version}; x64: {Environment.Is64BitProcess}; config: {path}");
                ListDevices(source);
                config.Validate();
                if (source.Connect(config))
                {
                    Console.WriteLine("DirectInput aperto: " + source.Selected!.ProductName);
                    foreach (var obj in source.Objects) Console.WriteLine(obj);
                    PrintState(source.Read(), config);
                }
                else Console.WriteLine("Backbone non trovato. Controlla joy.cpl, select e whitelist HidHide.");
                using var probe = new VirtualXbox();
                probe.Send(XboxReport.Neutral);
                Console.WriteLine("ViGEm: creazione e invio Xbox 360 riusciti. La verifica nei giochi resta da fare.");
                return 0;
            }
            if (command == "wizard")
            {
                if (!source.Connect(config)) throw new IOException("Controller non trovato: usa devices/select e verifica joy.cpl.");
                Wizard.Run(source, config, path, cancel.Token); return 0;
            }
            if (command == "run") config.Validate(true);
            using var instance = new Mutex(false, "Local\\BackboneXInput.Bridge");
            var ownsMutex = command != "run" || instance.WaitOne(0);
            if (!ownsMutex) throw new InvalidOperationException("Un bridge BackboneXInput e gia in esecuzione.");
            using var sink = command == "run" ? new OutputSession<XboxReport>(() => new VirtualXbox()) : null;
            using var timer = new TimerResolution();
            Log.Write($"{command}: {config.PollingHz} Hz richiesti; config {path}; Ctrl+C per uscire.");
            Loop(source, sink, config, cancel.Token);
            return 0;
        }
        catch (OperationCanceledException) { Log.Write("Arresto richiesto."); return 0; }
        catch (Exception ex) { Log.Write("Errore: " + ex.Message + "\nUsa help/diagnose; per ViGEm controlla installazione driver e riavvio.", ex); return 1; }
        finally
        {
            if (args.Contains("--pause") && !Console.IsInputRedirected)
            {
                Console.WriteLine("Premi Invio per chiudere lo strumento e riprendere il bridge.");
                Console.ReadLine();
            }
        }
    }

    public static void RunBackground(string path, Action<string> status, CancellationToken token)
    {
        var config = ConfigStore.Load(path);
        config.Validate(true);
        using var instance = new Mutex(false, "Local\\BackboneXInput.Bridge");
        bool owns;
        try { owns = instance.WaitOne(0); }
        catch (AbandonedMutexException) { owns = true; }
        if (!owns) throw new InvalidOperationException("Un bridge e gia in esecuzione. Chiudi run o l'altra app.");
        try
        {
            using var source = new DirectInputSource();
            using var sink = new OutputSession<XboxReport>(() => new VirtualXbox());
            using var timer = new TimerResolution();
            Loop(source, sink, config, token, status);
        }
        finally { instance.ReleaseMutex(); }
    }

    private static void Loop(DirectInputSource source, OutputSession<XboxReport>? sink, AppConfig config, CancellationToken token, Action<string>? status = null)
    {
        var clock = Stopwatch.StartNew();
        var interval = 1.0 / config.PollingHz;
        var due = 0.0;
        var nextMonitor = 0.0;
        var statsAt = 0.0;
        var reports = 0;
        var late = 0;
        var connected = false;
        status?.Invoke("In attesa del Backbone");
        while (!token.IsCancellationRequested)
        {
            if (!connected)
            {
                try
                {
                    connected = source.Connect(config);
                    if (connected) Log.Write("Connesso: " + source.Selected!.ProductName + " | " + source.Selected.InstanceGuid);
                }
                catch (InvalidOperationException) { throw; }
                catch (Exception ex) { Log.Write("Apertura input fallita; riprovo tra 1 secondo.", ex); }
                if (!connected) { token.WaitHandle.WaitOne(1000); continue; }
                due = clock.Elapsed.TotalSeconds;
                statsAt = due;
                reports = 0; late = 0;
            }
            RawState state;
            try { state = source.Read(); }
            catch (Exception ex)
            {
                sink?.Disconnect();
                source.Disconnect(); connected = false;
                status?.Invoke("In attesa del Backbone");
                Log.Write("Input perso: Xbox virtuale disconnesso, riconnessione tra 1 secondo.", ex);
                token.WaitHandle.WaitOne(1000); continue;
            }
            // Output failures must stop the bridge, rather than be mistaken for input loss.
            sink?.Send(Mapping.Convert(state, config));
            if (reports == 0) status?.Invoke("Backbone collegato - Xbox attivo");
            reports++;
            var now = clock.Elapsed.TotalSeconds;
            if (sink is null && now >= nextMonitor) { PrintState(state, config); nextMonitor = now + 0.1; }
            if (now - statsAt >= 5)
            {
                Log.Write($"Frequenza effettiva: {reports / (now - statsAt):F1} Hz; cicli in ritardo: {late}.");
                reports = 0; late = 0; statsAt = now;
            }
            due += interval;
            var remaining = due - clock.Elapsed.TotalSeconds;
            if (remaining <= 0) { late++; due = clock.Elapsed.TotalSeconds; }
            else token.WaitHandle.WaitOne(TimeSpan.FromSeconds(remaining));
        }
    }

    private static void ListDevices(DirectInputSource source)
    {
        var devices = source.Enumerate();
        for (var i = 0; i < devices.Count; i++) Console.WriteLine($"{i + 1}: {devices[i].ProductName} | {devices[i].InstanceName} | GUID {devices[i].InstanceGuid}");
        if (devices.Count == 0) Console.WriteLine("Nessun controller DirectInput collegato.");
    }

    private static void PrintState(RawState state, AppConfig config)
    {
        Console.WriteLine("\n" + DateTime.Now.ToString("HH:mm:ss.fff") + " Buttons (JSON/joy.cpl): " + string.Join(" ", state.Buttons.Select((v, i) => (v, i)).Where(p => p.v).Select(p => $"{p.i}/{p.i + 1}")));
        Console.WriteLine("Axes: " + string.Join(" | ", state.Axes.Select((v, i) => $"{i}:{DirectInputSource.AxisNames[i]} raw={v} nominal={Math.Clamp((v - 32767.5) / 32767.5, -1, 1):F3}")));
        Console.WriteLine("POV: " + string.Join(" | ", state.Pov.Select((v, i) => $"{i}={v}")));
        var report = Mapping.Convert(state, config);
        Console.WriteLine($"Xbox: LX={report.LeftX / 32768.0:F3} LY={report.LeftY / 32768.0:F3} RX={report.RightX / 32768.0:F3} RY={report.RightY / 32768.0:F3} LT={report.LT / 255.0:F3} RT={report.RT / 255.0:F3} [{string.Join(",", report.Buttons)}]");
    }

    private static void Help() => Console.WriteLine("BackboneXInput\nComandi: init, devices, select, monitor, wizard, run, diagnose\nOpzione: --config percorso.json\nPrimo uso: devices -> select (se necessario) -> monitor -> wizard -> run\nConfigurazione predefinita: %LOCALAPPDATA%\\BackboneXInput\\config.json\nDriver ViGEmBus necessario solo per run/diagnose. Leggi README per EOL e HidHide opzionale.");

    private sealed class TimerResolution : IDisposable
    {
        private readonly bool enabled = timeBeginPeriod(1) == 0;
        public void Dispose() { if (enabled) timeEndPeriod(1); }
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint milliseconds);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint milliseconds);
    }
}
