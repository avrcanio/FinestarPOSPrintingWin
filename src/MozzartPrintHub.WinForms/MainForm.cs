using System.Text;
using MozzartPrintHub.WinForms.Configuration;
using MozzartPrintHub.WinForms.Domain;
using MozzartPrintHub.WinForms.Services;

namespace MozzartPrintHub.WinForms;

public sealed class MainForm : Form
{
    private readonly AppSettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly EscPosParser _parser = new();
    private readonly ReceiptPreviewService _receiptPreviewService = new();
    private readonly PrinterService _printer = new();
    private AppSettings _settings;
    private EmulatorStore _store;

    private TcpEscPosServer? _tcpServer;
    private HttpReceiverServer? _httpServer;

    private readonly Button _startButton = new() { Text = "Start Listeners", Width = 140 };
    private readonly Button _stopButton = new() { Text = "Stop", Width = 80, Enabled = false };
    private readonly Button _settingsButton = new() { Text = "Settings", Width = 100 };
    private readonly ComboBox _printerCombo = new() { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = "Status: stopped" };
    private readonly ListBox _historyList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _previewBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 10)
    };

    public MainForm()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _settingsService = new AppSettingsService(settingsPath);
        _startupRegistrationService = new StartupRegistrationService("MozzartPrintHub");
        _settings = _settingsService.Load();
        _store = new EmulatorStore(Math.Max(10, _settings.Emulator.MaxHistoryEntries));

        Text = "Mozzart Print Hub";
        Width = 1100;
        Height = 700;
        BuildLayout();
        LoadPrintersAndApplyDefault();
        ApplyStartWithWindowsSetting();

        _startButton.Click += async (_, _) => await StartAsync();
        _stopButton.Click += (_, _) => Stop();
        _settingsButton.Click += (_, _) => OpenSettingsDialog();
        _historyList.SelectedIndexChanged += (_, _) => RenderSelectedPreview();
    }

    private void BuildLayout()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };
        top.Controls.Add(_startButton);
        top.Controls.Add(_stopButton);
        top.Controls.Add(_settingsButton);
        top.Controls.Add(new Label { Text = "Printer:", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        top.Controls.Add(_printerCombo);
        top.Controls.Add(_statusLabel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 360
        };

        split.Panel1.Controls.Add(_historyList);
        split.Panel2.Controls.Add(_previewBox);

        Controls.Add(split);
        Controls.Add(top);
    }

    private void LoadPrintersAndApplyDefault()
    {
        _printerCombo.Items.Clear();
        foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
        {
            _printerCombo.Items.Add(printer);
        }

        if (_printerCombo.Items.Count == 0)
        {
            return;
        }

        var configured = _settings.Print.DefaultPrinterName;
        var configuredIndex = _printerCombo.FindStringExact(configured);
        _printerCombo.SelectedIndex = configuredIndex >= 0 ? configuredIndex : 0;

        if (string.IsNullOrWhiteSpace(_settings.Print.DefaultPrinterName) && _printerCombo.SelectedItem is not null)
        {
            _settings.Print.DefaultPrinterName = _printerCombo.SelectedItem.ToString() ?? string.Empty;
            _settingsService.Save(_settings);
        }
    }

    private async Task StartAsync()
    {
        if (_tcpServer is not null)
        {
            return;
        }

        try
        {
            if (_settings.Emulator.Enabled)
            {
                _tcpServer = new TcpEscPosServer(
                    _settings.Emulator.Bind,
                    _settings.Emulator.Port,
                    _settings.Emulator.MaxJobBytes,
                    TimeSpan.FromSeconds(_settings.Emulator.IdleReadTimeoutSeconds),
                    OnPayloadAsync);
                await _tcpServer.StartAsync();
            }

            _httpServer = new HttpReceiverServer(
                _settings.Receiver.Bind,
                _settings.Receiver.Port,
                _store,
                OnPayloadAsync,
                _receiptPreviewService,
                RefreshUiAsync,
                _settings.Receiver.Token);
            await _httpServer.StartAsync();

            _statusLabel.Text = _settings.Emulator.Enabled
                ? $"Status: running (TCP {_settings.Emulator.Port}, HTTP {_settings.Receiver.Port})"
                : $"Status: running (HTTP {_settings.Receiver.Port})";
            _startButton.Enabled = false;
            _stopButton.Enabled = true;
        }
        catch (Exception ex)
        {
            Stop();
            MessageBox.Show(this, ex.Message, "Failed to start listeners", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Stop()
    {
        _tcpServer?.Stop();
        _httpServer?.Stop();
        _tcpServer = null;
        _httpServer = null;

        _statusLabel.Text = "Status: stopped";
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
    }

    private void OpenSettingsDialog()
    {
        var printers = _printerCombo.Items.Cast<object>().Select(p => p.ToString() ?? string.Empty).ToList();
        using var dialog = new SettingsForm(_settings, printers);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings = dialog.UpdatedSettings;
        _settingsService.Save(_settings);
        ApplyStartWithWindowsSetting();
        _store = new EmulatorStore(Math.Max(10, _settings.Emulator.MaxHistoryEntries));
        _historyList.Items.Clear();
        _previewBox.Clear();
        LoadPrintersAndApplyDefault();

        if (_tcpServer is not null || _httpServer is not null)
        {
            Stop();
            _ = StartAsync();
        }
    }

    private async Task OnPayloadAsync(byte[] payload, string endpoint)
    {
        var (doc, unknown) = _parser.Parse(payload);
        var source = endpoint.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ? "http" : "tcp9100";
        var clientIp = endpoint.Contains(':')
            ? endpoint[(endpoint.IndexOf(':') + 1)..]
            : endpoint;

        var job = new EmulatorJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = source,
            ClientIp = clientIp,
            RawSizeBytes = payload.Length,
            UnknownCommandCount = unknown,
            ReceivedAtUtc = DateTime.UtcNow,
            Document = doc
        };

        var printer = _printerCombo.InvokeRequired
            ? (string?)_printerCombo.Invoke(() => _printerCombo.SelectedItem?.ToString())
            : _printerCombo.SelectedItem?.ToString();

        if (_settings.Print.AutoPrint && !string.IsNullOrWhiteSpace(printer))
        {
            var printed = _printer.TryPrint(doc, printer, out var error);
            job.PrintStatus = printed ? "printed" : "failed";
            job.Error = error;
        }
        else if (!_settings.Print.AutoPrint)
        {
            job.PrintStatus = "preview_only";
        }
        else
        {
            job.PrintStatus = "failed";
            job.Error = "No printer selected";
        }

        _store.Add(job);
        await RefreshUiAsync(job);
    }

    private Task RefreshUiAsync(EmulatorJob job)
    {
        if (!IsHandleCreated)
        {
            return Task.CompletedTask;
        }

        BeginInvoke(() =>
        {
            _historyList.Items.Insert(0, $"{job.ReceivedAtUtc:HH:mm:ss} {job.Source} {job.PrintStatus} {job.ClientIp}");
            _historyList.SelectedIndex = 0;
            _statusLabel.Text = $"Status: running | Last: {job.PrintStatus}";
        });

        return Task.CompletedTask;
    }

    private void RenderSelectedPreview()
    {
        var selected = _store.GetHistory(200);
        if (_historyList.SelectedIndex < 0 || _historyList.SelectedIndex >= selected.Count)
        {
            return;
        }

        var job = selected[_historyList.SelectedIndex];
        var sb = new StringBuilder();
        sb.AppendLine($"Job: {job.Id}");
        sb.AppendLine($"When: {job.ReceivedAtUtc:O}");
        sb.AppendLine($"Source: {job.Source} ({job.ClientIp})");
        sb.AppendLine($"Bytes: {job.RawSizeBytes}");
        sb.AppendLine($"Unknown commands: {job.UnknownCommandCount}");
        sb.AppendLine($"Print: {job.PrintStatus}");
        if (!string.IsNullOrWhiteSpace(job.Error))
        {
            sb.AppendLine($"Error: {job.Error}");
        }

        sb.AppendLine();
        sb.AppendLine("Preview:");
        foreach (var line in job.Document.Lines)
        {
            sb.AppendLine($"{line.Align,-6} {(line.Bold ? "[B]" : "[ ]")} {line.Text}");
        }

        _previewBox.Text = sb.ToString();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Stop();
        base.OnFormClosing(e);
    }

    private void ApplyStartWithWindowsSetting()
    {
        try
        {
            _startupRegistrationService.Apply(_settings.Print.StartWithWindows, Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to update Start with Windows option.\n{ex.Message}",
                "Startup registration error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
