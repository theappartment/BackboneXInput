using System.Diagnostics;
using BackboneXInput.Core;
using Control = BackboneXInput.Core.Control;
using Binding = BackboneXInput.Core.Binding;

namespace BackboneXInput.Tray;

internal sealed class MappingForm : Form
{
    private static readonly Control[] controls = Enum.GetValues<Control>();
    private static readonly string[] axisNames = ["X", "Y", "Z", "RX", "RY", "RZ", "S1", "S2"];
    private readonly string path;
    private readonly AppConfig config;
    private readonly MappingDevice device = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 20 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ComboBox devices = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox steps = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.None };
    private readonly Label step = Label(11);
    private readonly Label heading = Label(28, true);
    private readonly Label instruction = Label(15);
    private readonly Label status = Label(11);
    private readonly Label detail = Label(9);
    private readonly ProgressBar countdown = new() { Dock = DockStyle.Fill, Maximum = 1000 };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Maximum = 20 };
    private readonly CommandPicture picture = new() { Dock = DockStyle.Fill };
    private readonly ListView summary = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, Visible = false };
    private readonly Button refresh = Button("Aggiorna", 100);
    private readonly Button start = Button("Rileva comando", 150);
    private readonly Button retry = Button("Riprova", 100);
    private readonly Button confirm = Button("Conferma e avanti", 165);
    private readonly Button previous = Button("Indietro", 100);
    private readonly Button skip = Button("Salta", 100);
    private readonly Button save = Button("Salva mapping", 150);
    private readonly Button cancel = Button("Annulla", 100);
    private MappingCapture capture = new(controls[0]);
    private CaptureStage shownStage;
    private int index;
    private bool connected;
    private bool saved;

    public MappingForm(string path)
    {
        this.path = path;
        config = File.Exists(path) ? ConfigStore.Load(path) : new AppConfig();
        config.Mappings.Clear();
        config.SkippedControls.Clear();
        Text = "BackboneXInput - Mapping guidato";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(940, 690);
        MinimumSize = new Size(900, 680);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        top.Controls.Add(new Label { Text = "Controller", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, 0);
        top.Controls.Add(devices, 1, 0); top.Controls.Add(refresh, 2, 0);
        root.Controls.Add(top, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(steps, 0, 0);
        var guide = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 0, 0), ColumnCount = 1, RowCount = 7 };
        foreach (var height in new[] { 30, 66, 200, 86, 62, 20, 44 })
            guide.RowStyles.Add(new RowStyle(height == 200 ? SizeType.Percent : SizeType.Absolute, height == 200 ? 100 : height));
        var preview = new Panel { Dock = DockStyle.Fill };
        summary.Columns.Add("Comando", 220); summary.Columns.Add("Input rilevato", 270);
        preview.Controls.Add(picture); preview.Controls.Add(summary);
        guide.Controls.Add(step, 0, 0); guide.Controls.Add(heading, 0, 1); guide.Controls.Add(preview, 0, 2);
        guide.Controls.Add(instruction, 0, 3); guide.Controls.Add(status, 0, 4);
        guide.Controls.Add(countdown, 0, 5); guide.Controls.Add(detail, 0, 6);
        body.Controls.Add(guide, 1, 0); root.Controls.Add(body, 0, 1); root.Controls.Add(progress, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        foreach (var button in new[] { cancel, save, confirm, start, retry, skip, previous }) actions.Controls.Add(button);
        root.Controls.Add(actions, 0, 3); Controls.Add(root);
        CancelButton = cancel;
        cancel.Click += (_, _) => Close();
        refresh.Click += (_, _) => RefreshDevices();
        start.Click += (_, _) => BeginCapture();
        retry.Click += (_, _) => ShowStep();
        confirm.Click += (_, _) => Confirm();
        skip.Click += (_, _) => { config.SkipControl(controls[index]); Advance(); };
        previous.Click += (_, _) => { if (index > 0) { index--; ShowStep(); } };
        summary.DoubleClick += (_, _) =>
        {
            if (summary.SelectedItems.Count == 1 && summary.SelectedItems[0].Tag is int target) { index = target; ShowStep(); }
        };
        save.Click += (_, _) => Save();
        timer.Tick += (_, _) => TickCapture();
        FormClosing += OnWizardClosing;
        Shown += (_, _) => { RefreshDevices(); ShowStep(); timer.Start(); };
    }

    private static Label Label(float size, bool bold = false) => new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), AutoEllipsis = false
    };
    private static Button Button(string text, int width) => new() { Text = text, Width = width, Height = 36, UseVisualStyleBackColor = true };

    private void RefreshDevices()
    {
        try
        {
            device.Disconnect(); connected = false;
            devices.Items.Clear();
            var available = device.Enumerate();
            devices.Items.AddRange(available);
            var matches = available.Where(d => config.DeviceInstanceGuid is { } guid ? d.InstanceGuid == guid : d.Name.Contains(config.DeviceNameContains, StringComparison.OrdinalIgnoreCase)).ToArray();
            devices.SelectedItem = matches.Length == 1 ? matches[0] : null;
            status.Text = available.Length == 0 ? "Collega il Backbone e premi Aggiorna." : "Scegli il Backbone nell'elenco in alto.";
        }
        catch (Exception ex) { status.Text = "Controller non disponibile: " + ex.Message; }
    }

    private void ShowStep()
    {
        capture = new(controls[index]); shownStage = CaptureStage.Idle;
        step.Text = $"Passo {index + 1} di {controls.Length}";
        heading.Text = ControlName(controls[index]);
        picture.Target = controls[index]; picture.Stage = CaptureStage.Idle; picture.Visible = true; picture.Invalidate();
        summary.Visible = false;
        instruction.Text = "Rilascia i tasti e centra le levette, poi premi Rileva comando.\nSe questo comando non c'e sul controller, premi Salta.";
        status.ForeColor = Color.FromArgb(55, 65, 75);
        status.Text = "Pronto per la rilevazione"; detail.Text = ""; countdown.Value = 0;
        start.Visible = true; start.Enabled = true; retry.Visible = false; confirm.Visible = false; save.Visible = false;
        skip.Visible = true;
        previous.Enabled = index > 0;
        AcceptButton = start;
        UpdateSteps();
        timer.Start();
    }

    private void BeginCapture()
    {
        try
        {
            if (!connected)
            {
                if (devices.SelectedItem is not MappingDeviceInfo selected) { status.Text = "Seleziona il Backbone nell'elenco in alto."; return; }
                if (!device.Connect(selected.InstanceGuid)) { status.Text = "Ricollega lo stesso Backbone. Se hai cambiato porta, riapri la guida."; return; }
                config.DeviceInstanceGuid = selected.InstanceGuid;
                connected = true;
            }
            devices.Enabled = false; refresh.Enabled = false;
            capture.Start(clock.Elapsed.TotalSeconds);
            start.Enabled = false; previous.Enabled = false; AcceptButton = null;
            RenderStage();
        }
        catch (Exception ex) { status.Text = "Impossibile leggere il controller: " + ex.Message; }
    }

    private void TickCapture()
    {
        if (!connected || index >= controls.Length) return;
        try
        {
            var raw = device.Read();
            capture.Push(raw, clock.Elapsed.TotalSeconds);
            if (capture.Stage != shownStage) RenderStage();
            if (controls[index] >= Control.LeftX && capture.Stage != CaptureStage.Review)
                detail.Text = string.Join("  ", raw.Axes.Select((value, i) => $"{axisNames[i]}: {value}"));
            if (capture.Stage is CaptureStage.Neutral or CaptureStage.Positive or CaptureStage.Opposite)
            {
                var remaining = capture.Remaining(clock.Elapsed.TotalSeconds);
                status.Text = $"Ancora {Math.Ceiling(remaining):0} secondi";
                countdown.Value = (int)Math.Clamp((1 - remaining / capture.Duration) * 1000, 0, 1000);
            }
            if (capture.Stage == CaptureStage.Review && capture.Result is { } b)
            {
                var level = controls[index] >= Control.LeftX ? Mapping.Stick(raw.Axes[b.Index], b) : Mapping.Activity(raw, b);
                detail.Text = $"Prova il comando: valore {level:F2}.  {Description(b)}";
            }
        }
        catch (Exception ex)
        {
            connected = false; device.Disconnect(); capture = new(controls[index]);
            instruction.Text = "Controller scollegato. Ricollega lo stesso Backbone e ripeti questo passo.";
            status.Text = ex.Message;
            start.Enabled = true; start.Visible = true; previous.Enabled = index > 0;
            confirm.Visible = false; retry.Visible = false; countdown.Value = 0; AcceptButton = start;
        }
    }

    private void RenderStage()
    {
        shownStage = capture.Stage;
        picture.Stage = capture.Stage; picture.Invalidate();
        switch (capture.Stage)
        {
            case CaptureStage.Neutral:
                instruction.Text = "Non toccare il controller: sto leggendo la posizione di riposo.";
                break;
            case CaptureStage.Positive:
                instruction.Text = Movement(controls[index], false);
                break;
            case CaptureStage.Opposite:
                instruction.Text = Movement(controls[index], true);
                break;
            case CaptureStage.Review:
                instruction.Text = "Comando rilevato. Puoi provarlo ancora.\nSe hai premuto quello giusto, conferma; altrimenti riprova.";
                status.Text = "Rilevato: " + Description(capture.Result!); status.ForeColor = Color.DarkGreen;
                countdown.Value = 1000;
                start.Visible = false; retry.Visible = true; confirm.Visible = true; previous.Enabled = index > 0;
                AcceptButton = confirm;
                break;
            case CaptureStage.Failed:
                instruction.Text = capture.Error; status.Text = "Premi Riprova per ripetere questo passo.";
                status.ForeColor = Color.Firebrick;
                start.Visible = false; retry.Visible = true; previous.Enabled = index > 0; AcceptButton = retry;
                break;
        }
    }

    private void Confirm()
    {
        if (capture.Stage != CaptureStage.Review || capture.Result is null) return;
        config.SetMapping(controls[index], capture.Result);
        Advance();
    }

    private void Advance()
    {
        index++;
        if (index < controls.Length) { ShowStep(); return; }
        timer.Stop(); device.Disconnect(); connected = false;
        UpdateSteps();
        step.Text = "20 di 20 completati"; heading.Text = "Mapping completato"; picture.Visible = false;
        summary.Items.Clear();
        for (var i = 0; i < controls.Length; i++)
        {
            var row = new ListViewItem(ControlName(controls[i])) { Tag = i };
            row.SubItems.Add(config.Mappings.TryGetValue(controls[i], out var binding) ? Description(binding) : "Saltato (disattivato)"); summary.Items.Add(row);
        }
        summary.Visible = true;
        instruction.Text = "Controlla il riepilogo e premi Salva mapping.\nPer rifare un comando, fai doppio clic sulla sua riga.";
        detail.Text = ""; status.Text = "Pronto per salvare";
        if (config.Mappings.TryGetValue(Control.LT, out var lt) && config.Mappings.TryGetValue(Control.RT, out var rt)
            && lt.Kind == InputKind.Axis && rt.Kind == InputKind.Axis && lt.Index == rt.Index)
            status.Text = "LT e RT condividono un asse: verifica anche la pressione simultanea nel monitor.";
        start.Visible = false; confirm.Visible = false; retry.Visible = false; save.Visible = true;
        skip.Visible = false; save.Enabled = config.Mappings.Count > 0;
        if (!save.Enabled) status.Text = "Associa almeno un comando: fai doppio clic su una riga per rifarlo.";
        previous.Enabled = true; AcceptButton = save;
    }

    private void UpdateSteps()
    {
        steps.Items.Clear();
        foreach (var c in controls) steps.Items.Add((index < controls.Length && c == controls[index] ? "> " : "  ") + (config.Mappings.ContainsKey(c) ? "OK " : config.SkippedControls.Contains(c) ? "-- " : "     ") + ControlName(c));
        progress.Value = config.ResolvedCount;
    }

    private void Save()
    {
        try
        {
            config.Validate(true);
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            ConfigStore.Save(path, config); saved = true; DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(this, "Non riesco a salvare: " + ex.Message, "Mapping", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OnWizardClosing(object? sender, FormClosingEventArgs e)
    {
        if (!saved && config.ResolvedCount > 0 && e.CloseReason == CloseReason.UserClosing)
            e.Cancel = MessageBox.Show(this, "Chiudere senza salvare il nuovo mapping?", "Mapping non salvato", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes;
    }

    internal static string ControlName(Control c) => c switch
    {
        Control.Up => "D-pad: su", Control.Down => "D-pad: giu", Control.Left => "D-pad: sinistra", Control.Right => "D-pad: destra",
        Control.L3 => "L3 (levetta SX)", Control.R3 => "R3 (levetta DX)",
        Control.LeftX => "Stick sinistro X", Control.LeftY => "Stick sinistro Y", Control.RightX => "Stick destro X", Control.RightY => "Stick destro Y",
        Control.Menu => "Menu / Start", Control.View => "View / Select", _ => c.ToString()
    };

    private static string Movement(Control c, bool opposite)
    {
        if (c >= Control.LeftX)
        {
            var stick = c is Control.LeftX or Control.LeftY ? "SINISTRA" : "DESTRA";
            var horizontal = c is Control.LeftX or Control.RightX;
            var direction = horizontal ? (opposite ? "SINISTRA" : "DESTRA") : (opposite ? "il BASSO" : "l'ALTO");
            return $"Porta la levetta {stick} completamente verso {direction}.\nTienila li fino alla prossima indicazione.";
        }
        if (c is Control.LT or Control.RT) return $"Premi il grilletto {c} fino in fondo e tienilo premuto.";
        if (c is Control.L3 or Control.R3) return $"Premi la levetta {(c == Control.L3 ? "sinistra" : "destra")} come un pulsante e tienila premuta.";
        return "Premi soltanto " + ControlName(c) + " e tienilo premuto.";
    }

    private static string Description(Binding b) => b.Kind switch
    {
        InputKind.Button => $"Pulsante {b.Index + 1}",
        InputKind.Pov => "D-pad rilevato",
        _ => "Asse analogico " + new[] { "X", "Y", "Z", "RX", "RY", "RZ", "Slider 1", "Slider 2" }[b.Index]
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); device.Dispose(); }
        base.Dispose(disposing);
    }
}
