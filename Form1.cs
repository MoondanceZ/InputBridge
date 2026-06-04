using System.Diagnostics;
using QRCoder;

namespace InputBridge;

public partial class Form1 : Form
{
    private readonly Label _titleLabel = new();
    private readonly PictureBox _qrBox = new();
    private readonly LinkLabel _urlLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _activityLabel = new();
    private readonly Button _settingsButton = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly Icon _appIcon;
    private readonly InputSimulator _input;
    private readonly GlobalInputWatcher _watcher;
    private AppSettings _settings;
    private SyncServer? _server;

    public Form1()
    {
        InitializeComponent();
        _appIcon = LoadAppIcon();
        _settings = AppSettings.Load();
        _input = new InputSimulator(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        _watcher = new GlobalInputWatcher();
        _watcher.ExternalInput += () => _server?.NotifyExternalInput();

        BuildUi();
        RefreshConnectionInfo();
        _ = StartServerAsync();
    }

    private void BuildUi()
    {
        Text = "InputBridge";
        Icon = _appIcon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        ClientSize = new Size(360, 485);

        _settingsButton.Text = "自定义";
        _settingsButton.Location = new Point(20, 18);
        _settingsButton.Size = new Size(80, 30);
        _settingsButton.TabStop = false;
        _settingsButton.MouseClick += (_, _) => ShowSettingsDialog();
        Controls.Add(_settingsButton);

        _versionLabel.Text = $"v{AppVersion.Current}";
        _versionLabel.ForeColor = Color.Gray;
        _versionLabel.Location = new Point(240, 18);
        _versionLabel.Size = new Size(100, 30);
        _versionLabel.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(_versionLabel);

        _titleLabel.Text = "手机扫码立即连接";
        _titleLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _titleLabel.Location = new Point(20, 62);
        _titleLabel.Size = new Size(320, 28);
        Controls.Add(_titleLabel);

        _qrBox.Location = new Point(80, 102);
        _qrBox.Size = new Size(200, 200);
        _qrBox.SizeMode = PictureBoxSizeMode.Zoom;
        Controls.Add(_qrBox);

        _urlLabel.Location = new Point(20, 320);
        _urlLabel.Size = new Size(320, 26);
        _urlLabel.TextAlign = ContentAlignment.MiddleCenter;
        _urlLabel.LinkClicked += (_, _) => OpenUrl(_settings.Url);
        Controls.Add(_urlLabel);

        _statusLabel.Text = "● 正在启动服务...";
        _statusLabel.ForeColor = Color.DarkOrange;
        _statusLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _statusLabel.Location = new Point(20, 350);
        _statusLabel.Size = new Size(320, 28);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_statusLabel);

        _activityLabel.Text = "同步服务已就绪";
        _activityLabel.ForeColor = Color.DimGray;
        _activityLabel.Location = new Point(12, 378);
        _activityLabel.Size = new Size(336, 24);
        _activityLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_activityLabel);

        var separator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(30, 408),
            Size = new Size(300, 2)
        };
        Controls.Add(separator);

        var tip = new Label
        {
            Text = "提示：点击最小化会缩小到托盘\r\n请确保手机与电脑处于同一 Wi-Fi",
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 422),
            Size = new Size(320, 42)
        };
        Controls.Add(tip);

        _trayIcon.Text = "InputBridge";
        _trayIcon.Icon = _appIcon;
        _trayIcon.Visible = true;
        _trayIcon.ContextMenuStrip = new ContextMenuStrip();
        _trayIcon.ContextMenuStrip.Items.Add("显示窗口", null, (_, _) => RestoreWindow());
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, async (_, _) => await QuitAsync());
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        };
    }

    private async Task StartServerAsync()
    {
        try
        {
            _server = new SyncServer(_settings, _input, UpdateStatus, UpdateSyncActivity);
            await _server.StartAsync();
            UpdateStatus(false);
        }
        catch (Exception ex)
        {
            BeginInvoke(() =>
            {
                _statusLabel.Text = $"● 服务启动失败：{ex.Message}";
                _statusLabel.ForeColor = Color.Red;
            });
        }
    }

    private async Task RestartServerAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }

        await StartServerAsync();
        RefreshConnectionInfo();
    }

    private void RefreshConnectionInfo()
    {
        _urlLabel.Text = _settings.Url;
        _qrBox.Image?.Dispose();
        _qrBox.Image = CreateQrImage(_settings.Url);
    }

    private void UpdateStatus(bool connected)
    {
        if (IsDisposed)
        {
            return;
        }

        void Apply()
        {
            _statusLabel.Text = connected ? "● 手机已连接" : "● 等待手机连接...";
            _statusLabel.ForeColor = connected ? Color.ForestGreen : Color.Red;
        }

        if (IsHandleCreated)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private void UpdateSyncActivity(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        void Apply()
        {
            _activityLabel.Text = $"{DateTime.Now:HH:mm:ss} 已同步";
            _activityLabel.ForeColor = Color.DimGray;
        }

        if (IsHandleCreated)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private void ShowSettingsDialog()
    {
        var wasTopMost = TopMost;
        TopMost = false;

        using var dialog = new Form
        {
            Text = "自定义",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(440, 360),
            ShowInTaskbar = false,
            TopMost = wasTopMost,
            Icon = _appIcon
        };
        dialog.Shown += (_, _) =>
        {
            dialog.BringToFront();
            dialog.Activate();
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 8
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        for (var i = 0; i < 7; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 1 ? 46 : 38));
        }
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dialog.Controls.Add(panel);

        var ipBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            IntegralHeight = false
        };
        ipBox.Items.Add("");
        foreach (var ip in AppSettings.GetLocalIpCandidates())
        {
            ipBox.Items.Add(ip);
        }
        ipBox.Text = _settings.Ip;
        AddRow(panel, 0, "局域网IP：", ipBox, "留空自动");

        var currentIp = new Label
        {
            Text = $"当前二维码地址：{_settings.Url}",
            AutoEllipsis = true,
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(currentIp, 1, 1);
        panel.SetColumnSpan(currentIp, 2);

        var portBox = AddTextRow(panel, 2, "端口号：", _settings.Port.ToString());
        var backspaceBox = AddTextRow(panel, 3, "退格限制：", _settings.BackspaceLimit.ToString(), "次");
        var autoClear = new CheckBox
        {
            Text = "自动清空",
            Checked = _settings.AutoClear,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        AddRow(panel, 4, "自动清空：", autoClear);
        var autoClearTimeBox = AddTextRow(panel, 5, "清空时间：", _settings.AutoClearTime.ToString(), "秒");
        var smartDetection = new CheckBox
        {
            Text = "智能感知重置",
            Checked = _settings.SmartDetection,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        AddRow(panel, 6, "智能感知：", smartDetection);

        var save = new Button
        {
            Text = "保存设置",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Size = new Size(100, 32)
        };
        panel.Controls.Add(save, 2, 7);
        dialog.AcceptButton = save;

        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (!int.TryParse(portBox.Text, out var port) || port is < 1 or > 65535)
            {
                MessageBox.Show(dialog, "端口号必须是 1-65535。", "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(backspaceBox.Text, out var backspaceLimit) || backspaceLimit < 0)
            {
                MessageBox.Show(dialog, "退格限制必须是非负整数。", "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(autoClearTimeBox.Text, out var autoClearTime) || autoClearTime < 1)
            {
                MessageBox.Show(dialog, "清空时间必须大于 0 秒。", "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var oldPort = _settings.Port;
            _settings.Ip = ipBox.Text.Trim();
            _settings.Port = port;
            _settings.BackspaceLimit = backspaceLimit;
            _settings.AutoClear = autoClear.Checked;
            _settings.AutoClearTime = autoClearTime;
            _settings.SmartDetection = smartDetection.Checked;
            _settings.Save();

            if (oldPort != _settings.Port)
            {
                _ = RestartServerAsync();
            }
            else
            {
                _server?.UpdateSettings(_settings);
                RefreshConnectionInfo();
            }
        }
        finally
        {
            TopMost = wasTopMost;
            BringToFront();
        }
    }

    private static TextBox AddTextRow(TableLayoutPanel parent, int row, string label, string value, string? suffix = null)
    {
        var box = new TextBox
        {
            Text = value,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        AddRow(parent, row, label, box, suffix);
        return box;
    }

    private static void AddRow(TableLayoutPanel parent, int row, string label, Control input, string? suffix = null)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        }, 0, row);

        parent.Controls.Add(input, 1, row);

        if (string.IsNullOrWhiteSpace(suffix))
        {
            return;
        }

        parent.Controls.Add(new Label
        {
            Text = suffix,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 2, row);
    }

    private static Image CreateQrImage(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(8);
        using var stream = new MemoryStream(bytes);
        return Image.FromStream(stream);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private static Icon LoadAppIcon()
    {
        var embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        return embeddedIcon ?? SystemIcons.Application;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async Task QuitAsync()
    {
        _trayIcon.Visible = false;
        _watcher.Dispose();
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }

        Application.Exit();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        _trayIcon.Visible = false;
        _watcher.Dispose();
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }

        base.OnFormClosing(e);
    }
}

