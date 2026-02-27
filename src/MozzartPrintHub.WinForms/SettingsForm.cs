using MozzartPrintHub.WinForms.Configuration;

namespace MozzartPrintHub.WinForms;

public sealed class SettingsForm : Form
{
    private readonly TextBox _receiverBind = new() { Width = 220 };
    private readonly NumericUpDown _receiverPort = new() { Minimum = 1, Maximum = 65535, Width = 100 };
    private readonly TextBox _receiverToken = new() { Width = 220 };

    private readonly CheckBox _emulatorEnabled = new() { Text = "Enable TCP emulator" };
    private readonly TextBox _emulatorBind = new() { Width = 220 };
    private readonly NumericUpDown _emulatorPort = new() { Minimum = 1, Maximum = 65535, Width = 100 };
    private readonly NumericUpDown _emulatorHistory = new() { Minimum = 10, Maximum = 2000, Width = 100 };
    private readonly NumericUpDown _emulatorMaxJob = new() { Minimum = 1024, Maximum = 4_000_000, Width = 100 };
    private readonly NumericUpDown _emulatorIdle = new() { Minimum = 1, Maximum = 120, Width = 100 };

    private readonly ComboBox _defaultPrinter = new() { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _autoPrint = new() { Text = "Auto print incoming jobs" };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows (current user)" };

    public AppSettings UpdatedSettings { get; private set; }

    public SettingsForm(AppSettings settings, IReadOnlyList<string> installedPrinters)
    {
        UpdatedSettings = CloneSettings(settings);

        Text = "Settings";
        Width = 560;
        Height = 510;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildLayout();
        Bind(settings, installedPrinters);
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(12),
            AutoSize = true
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        AddRow(layout, "Receiver bind:", _receiverBind);
        AddRow(layout, "Receiver port:", _receiverPort);
        AddRow(layout, "Receiver token:", _receiverToken);
        AddRow(layout, string.Empty, _emulatorEnabled);
        AddRow(layout, "Emulator bind:", _emulatorBind);
        AddRow(layout, "Emulator port:", _emulatorPort);
        AddRow(layout, "Max history:", _emulatorHistory);
        AddRow(layout, "Max job bytes:", _emulatorMaxJob);
        AddRow(layout, "Idle timeout (s):", _emulatorIdle);
        AddRow(layout, "Default printer:", _defaultPrinter);
        AddRow(layout, string.Empty, _autoPrint);
        AddRow(layout, string.Empty, _startWithWindows);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(12, 4, 12, 4)
        };

        var save = new Button { Text = "Save", Width = 96 };
        var cancel = new Button { Text = "Cancel", Width = 96 };
        save.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        var labelControl = new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        layout.Controls.Add(labelControl, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void Bind(AppSettings settings, IReadOnlyList<string> installedPrinters)
    {
        _receiverBind.Text = settings.Receiver.Bind;
        _receiverPort.Value = settings.Receiver.Port;
        _receiverToken.Text = settings.Receiver.Token;

        _emulatorEnabled.Checked = settings.Emulator.Enabled;
        _emulatorBind.Text = settings.Emulator.Bind;
        _emulatorPort.Value = settings.Emulator.Port;
        _emulatorHistory.Value = settings.Emulator.MaxHistoryEntries;
        _emulatorMaxJob.Value = settings.Emulator.MaxJobBytes;
        _emulatorIdle.Value = settings.Emulator.IdleReadTimeoutSeconds;

        foreach (var printer in installedPrinters)
        {
            _defaultPrinter.Items.Add(printer);
        }

        if (_defaultPrinter.Items.Count > 0)
        {
            var idx = _defaultPrinter.FindStringExact(settings.Print.DefaultPrinterName);
            _defaultPrinter.SelectedIndex = idx >= 0 ? idx : 0;
        }

        _autoPrint.Checked = settings.Print.AutoPrint;
        _startWithWindows.Checked = settings.Print.StartWithWindows;
    }

    private void SaveAndClose()
    {
        UpdatedSettings = new AppSettings
        {
            Receiver = new ReceiverSettings
            {
                Bind = string.IsNullOrWhiteSpace(_receiverBind.Text) ? "0.0.0.0" : _receiverBind.Text.Trim(),
                Port = (int)_receiverPort.Value,
                Token = _receiverToken.Text.Trim()
            },
            Emulator = new EmulatorSettings
            {
                Enabled = _emulatorEnabled.Checked,
                Bind = string.IsNullOrWhiteSpace(_emulatorBind.Text) ? "0.0.0.0" : _emulatorBind.Text.Trim(),
                Port = (int)_emulatorPort.Value,
                MaxHistoryEntries = (int)_emulatorHistory.Value,
                MaxJobBytes = (int)_emulatorMaxJob.Value,
                IdleReadTimeoutSeconds = (int)_emulatorIdle.Value
            },
            Print = new PrintSettings
            {
                DefaultPrinterName = _defaultPrinter.SelectedItem?.ToString() ?? string.Empty,
                AutoPrint = _autoPrint.Checked,
                StartWithWindows = _startWithWindows.Checked
            }
        };

        DialogResult = DialogResult.OK;
    }

    private static AppSettings CloneSettings(AppSettings value)
    {
        return new AppSettings
        {
            Receiver = new ReceiverSettings
            {
                Bind = value.Receiver.Bind,
                Port = value.Receiver.Port,
                Token = value.Receiver.Token
            },
            Emulator = new EmulatorSettings
            {
                Enabled = value.Emulator.Enabled,
                Bind = value.Emulator.Bind,
                Port = value.Emulator.Port,
                MaxHistoryEntries = value.Emulator.MaxHistoryEntries,
                MaxJobBytes = value.Emulator.MaxJobBytes,
                IdleReadTimeoutSeconds = value.Emulator.IdleReadTimeoutSeconds
            },
            Print = new PrintSettings
            {
                DefaultPrinterName = value.Print.DefaultPrinterName,
                AutoPrint = value.Print.AutoPrint,
                StartWithWindows = value.Print.StartWithWindows
            }
        };
    }
}
