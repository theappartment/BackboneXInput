using System.Diagnostics;
using Microsoft.Win32;

namespace BackboneXInput.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var single = new Mutex(false, "Local\\BackboneXInput.Tray");
        bool owns;
        try { owns = single.WaitOne(0); }
        catch (AbandonedMutexException) { owns = true; }
        if (!owns) { MessageBox.Show("BackboneXInput e gia aperto vicino all'orologio."); return; }
        try
        {
            ApplicationConfiguration.Initialize();
            using var context = new TrayContext(args.Contains("--mapping"));
            Application.Run(context);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BackboneXInput", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { single.ReleaseMutex(); }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "BackboneXInput";
    private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackboneXInput", "config.json");
    private readonly Form dispatcher = new();
    private readonly NotifyIcon icon;
    private readonly ToolStripMenuItem state = new("Avvio...") { Enabled = false };
    private readonly ToolStripMenuItem startup = new("Avvia con Windows") { CheckOnClick = false };
    private readonly ContextMenuStrip menu = new();
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private bool toolOpen;
    private bool quitting;

    public TrayContext(bool openMapping = false)
    {
        _ = dispatcher.Handle;
        menu.Items.Add(state);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Mapping guidato", null, async (_, _) => await RunMapping());
        AddTool("Seleziona controller", "select");
        AddTool("Input Monitor", "monitor");
        AddTool("Diagnostica", "diagnose");
        startup.Click += (_, _) => ToggleStartup();
        menu.Items.Add(startup);
        menu.Items.Add("Apri log", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, async (_, _) => await Quit());
        menu.Opening += (_, _) => RefreshStartup();
        icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "BackboneXInput", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += async (_, _) => await RunMapping();
        StartWorker();
        if (openMapping) dispatcher.BeginInvoke((Action)(async () => await RunMapping()));
    }

    private void StartWorker()
    {
        cancellation = new();
        var token = cancellation.Token;
        worker = Task.Factory.StartNew(() =>
        {
            string? lastError = null;
            while (!token.IsCancellationRequested)
            {
                try { BackboneXInput.Program.RunBackground(configPath, Status, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Status(File.Exists(configPath) ? "Errore: " + ex.Message : "Completa prima il Mapping Wizard");
                    if (lastError != ex.ToString()) { WriteError(ex); lastError = ex.ToString(); }
                }
                if (token.WaitHandle.WaitOne(5000)) break;
            }
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void Status(string text)
    {
        if (quitting || dispatcher.IsDisposed) return;
        try
        {
            dispatcher.BeginInvoke(() =>
            {
                if (quitting) return;
                state.Text = text;
                var tooltip = "BackboneXInput: " + text;
                icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
            });
        }
        catch (InvalidOperationException) { }
    }

    private void AddTool(string text, string command)
        => menu.Items.Add(text, null, async (_, _) => await RunTool(command));

    private async Task RunMapping()
    {
        if (toolOpen || quitting) return;
        toolOpen = true;
        try
        {
            await StopWorker();
            state.Text = "Mapping guidato in corso";
            menu.Enabled = false;
            using var wizard = new MappingForm(configPath);
            wizard.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Mapping", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { menu.Enabled = true; toolOpen = false; if (!quitting) StartWorker(); }
    }

    private async Task StopWorker()
    {
        cancellation?.Cancel();
        if (worker is not null)
        {
            try { await worker; }
            catch (OperationCanceledException) { }
        }
        worker = null;
        cancellation?.Dispose();
        cancellation = null;
    }

    private async Task RunTool(string command)
    {
        if (toolOpen || quitting) return;
        toolOpen = true;
        try
        {
            await StopWorker();
            state.Text = "Configurazione in corso";
            var exe = Path.Combine(AppContext.BaseDirectory, "BackboneXInput.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("Estrai tutto il pacchetto: BackboneXInput.exe mancante.");
            var info = new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory };
            info.ArgumentList.Add(command);
            info.ArgumentList.Add("--config");
            info.ArgumentList.Add(configPath);
            info.ArgumentList.Add("--pause");
            using var child = Process.Start(info) ?? throw new IOException("Impossibile aprire lo strumento.");
            await child.WaitForExitAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BackboneXInput", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { toolOpen = false; if (!quitting) StartWorker(); }
    }

    private void RefreshStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            startup.Checked = key?.GetValue(RunName) as string == StartupCommand();
        }
        catch (Exception ex) { WriteError(ex); startup.Checked = false; }
    }

    private static string StartupCommand() => "\"" + Application.ExecutablePath + "\"";

    private void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (startup.Checked) key.DeleteValue(RunName, false);
            else key.SetValue(RunName, StartupCommand(), RegistryValueKind.String);
            RefreshStartup();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Avvio automatico", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void OpenLogs()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackboneXInput", "logs");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Log"); }
    }

    private static void WriteError(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackboneXInput", "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "tray.log");
            if (File.Exists(path) && new FileInfo(path).Length > 5_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + ex + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private async Task Quit()
    {
        if (quitting) return;
        quitting = true;
        await StopWorker();
        icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            quitting = true;
            cancellation?.Cancel();
            try { worker?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            cancellation?.Dispose();
            icon.Dispose();
            menu.Dispose();
            dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
