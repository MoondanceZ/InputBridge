using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaBrushes = Avalonia.Media.Brushes;
using AvaColor = Avalonia.Media.Color;
using AvaCursor = Avalonia.Input.Cursor;
using AvaHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using AvaOrientation = Avalonia.Layout.Orientation;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using QRCoder;
using AvaButton = Avalonia.Controls.Button;
using AvaCheckBox = Avalonia.Controls.CheckBox;
using AvaControl = Avalonia.Controls.Control;
using AvaImage = Avalonia.Controls.Image;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using AvaTextBox = Avalonia.Controls.TextBox;

namespace InputBridge;

public sealed class MainWindow : Window
{
    private readonly AvaImage _qrImage = new();
    private readonly TextBlock _urlText = new();
    private readonly TextBlock _wifiHintText = new();
    private readonly TextBlock _statusText = new();
    private readonly Border _statusPill = new();
    private readonly TextBlock _activityText = new();
    private readonly Border _settingsOverlay = new();
    private readonly Border _closeOverlay = new();
    private readonly AvaTextBox _settingsIpBox = new();
    private readonly AvaTextBox _settingsPortBox = new();
    private readonly AvaTextBox _settingsBackspaceBox = new();
    private readonly AvaTextBox _settingsAutoClearTimeBox = new();
    private readonly AvaCheckBox _settingsAutoClearBox = new();
    private readonly AvaCheckBox _settingsSmartDetectionBox = new();
    private readonly TextBlock _settingsErrorText = new();
    private readonly TextBlock _settingsUrlText = new();
    private readonly TrayIcon _trayIcon = new();
    private readonly AvaBitmap _appIconBitmap;
    private readonly WindowIcon? _trayWindowIcon;
    private readonly InputSimulator _input;
    private GlobalInputWatcher? _watcher;
    private AppSettings _settings;
    private SyncServer? _server;
    private bool _quitting;
    private bool _allowClose;
    private bool _exitStarted;
    private bool _watcherDisposed;
    private bool _trayDisposed;

    public MainWindow()
    {
        _appIconBitmap = LoadAppIconBitmap();
        _trayWindowIcon = LoadWindowIcon();
        _settings = AppSettings.Load();
        _input = new InputSimulator();

        BuildWindow();
        QueueStartupWork();
    }

    private void BuildWindow()
    {
        Title = "InputBridge";
        Icon = _trayWindowIcon;
        Width = 390;
        Height = 520;
        MinWidth = 390;
        MinHeight = 520;
        CanResize = false;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        Background = AvaBrushes.Transparent;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.None
        ];

        var surface = new Border
        {
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(AvaColor.FromRgb(248, 251, 255), 0),
                    new GradientStop(AvaColor.FromRgb(233, 240, 249), 1)
                }
            },
            BorderBrush = Brush("#D8E1EC"),
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 34,
                Spread = 0,
                OffsetY = 18,
                Color = AvaColor.FromArgb(45, 23, 38, 59)
            }),
            Child = BuildContent()
        };
        surface.AddHandler(PointerPressedEvent, DragWindowFromTopArea, RoutingStrategies.Tunnel);

        Content = surface;
    }

    private AvaControl BuildContent()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(82)),
                new RowDefinition(GridLength.Star)
            }
        };

        root.Children.Add(BuildTitleBar());

        var body = new Grid
        {
            Margin = new Thickness(24, 0, 24, 24),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(12)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        Grid.SetRow(body, 1);
        body.Children.Add(BuildConnectionCard());

        var status = BuildStatusPanel();
        Grid.SetRow(status, 2);
        body.Children.Add(status);

        var preview = BuildInputPreviewPanel();
        Grid.SetRow(preview, 4);
        body.Children.Add(preview);

        root.Children.Add(body);
        root.Children.Add(BuildSettingsOverlay());
        root.Children.Add(BuildCloseOverlay());
        return root;
    }

    private AvaControl BuildTitleBar()
    {
        var bar = new Grid
        {
            Background = AvaBrushes.Transparent,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(168))
            },
            Margin = new Thickness(24, 18, 18, 12)
        };
        bar.AddHandler(PointerPressedEvent, DragWindow, RoutingStrategies.Tunnel);

        var mark = new AvaImage
        {
            Width = 42,
            Height = 42,
            Source = _appIconBitmap,
            Stretch = Stretch.Uniform
        };
        bar.Children.Add(mark);

        var titleStack = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleStack, 1);
        titleStack.Children.Add(new TextBlock
        {
            Text = "InputBridge",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"手机输入桥 · v{AppVersion.Current}",
            FontSize = 12,
            Foreground = Brush("#718096")
        });
        bar.Children.Add(titleStack);

        var windowButtons = new StackPanel
        {
            Orientation = AvaOrientation.Horizontal,
            HorizontalAlignment = AvaHorizontalAlignment.Right,
            Spacing = 8
        };
        Grid.SetColumn(windowButtons, 2);
        windowButtons.Children.Add(HeaderButton("设置", ShowSettingsDrawer));
        windowButtons.Children.Add(ChromeButton("─", Hide));
        windowButtons.Children.Add(ChromeButton("×", ConfirmAndCloseAsync));
        bar.Children.Add(windowButtons);

        return bar;
    }

    private AvaControl BuildConnectionCard()
    {
        var card = Card();
        card.Padding = new Thickness(16);

        var stack = new StackPanel
        {
            Spacing = 10
        };

        stack.Children.Add(new TextBlock
        {
            Text = "手机扫码立即连接",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });

        _wifiHintText.Text = "请保持手机和电脑处于同一 Wi-Fi";
        _wifiHintText.FontSize = 13;
        _wifiHintText.Foreground = Brush("#718096");
        _wifiHintText.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(_wifiHintText);

        var qrShell = new Border
        {
            Width = 176,
            Height = 176,
            CornerRadius = new CornerRadius(14),
            Background = AvaBrushes.White,
            BorderBrush = Brush("#E3E9F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            HorizontalAlignment = AvaHorizontalAlignment.Center,
            Child = _qrImage
        };
        stack.Children.Add(qrShell);

        var urlPanel = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = Brush("#F4F7FB"),
            BorderBrush = Brush("#DDE6F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 9)
        };

        _urlText.FontSize = 13;
        _urlText.Foreground = Brush("#1769E0");
        _urlText.VerticalAlignment = VerticalAlignment.Center;
        _urlText.TextTrimming = TextTrimming.CharacterEllipsis;
        _urlText.Cursor = new AvaCursor(StandardCursorType.Hand);
        _urlText.Text = _settings.Url;
        _urlText.PointerPressed += (_, _) => OpenUrl(_settings.Url);
        urlPanel.Child = _urlText;
        stack.Children.Add(urlPanel);

        card.Child = stack;
        return card;
    }

    private AvaControl BuildStatusPanel()
    {
        _statusPill.CornerRadius = new CornerRadius(14);
        _statusPill.Padding = new Thickness(16, 10);
        _statusPill.Child = _statusText;
        _statusText.FontSize = 15;
        _statusText.FontWeight = FontWeight.SemiBold;
        _statusText.Text = "● 等待手机连接";
        _statusText.Foreground = Brush("#C13830");
        _statusPill.Background = Brush("#FFF1F0");
        return _statusPill;
    }

    private AvaControl BuildInputPreviewPanel()
    {
        _activityText.Text = "";
        _activityText.FontSize = 13;
        _activityText.FontWeight = FontWeight.SemiBold;
        _activityText.Foreground = Brush("#536174");
        _activityText.TextTrimming = TextTrimming.None;
        _activityText.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(GridLength.Star)
            }
        };

        grid.Children.Add(new TextBlock
        {
            Text = "输入预览",
            FontSize = 12,
            Foreground = Brush("#718096"),
            VerticalAlignment = VerticalAlignment.Center
        });

        Grid.SetColumn(_activityText, 1);
        grid.Children.Add(_activityText);

        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brush("#F7FAFE"),
            BorderBrush = Brush("#E1E8F0"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 9),
            Child = grid
        };
    }

    private AvaControl BuildSettingsOverlay()
    {
        _settingsOverlay.IsVisible = false;
        _settingsOverlay.Background = OverlayBrush();
        _settingsOverlay.Child = BuildSettingsDrawer();
        Grid.SetRowSpan(_settingsOverlay, 2);
        return _settingsOverlay;
    }

    private AvaControl BuildCloseOverlay()
    {
        _closeOverlay.IsVisible = false;
        _closeOverlay.Background = OverlayBrush();
        _closeOverlay.Child = BuildClosePrompt();
        Grid.SetRowSpan(_closeOverlay, 2);
        return _closeOverlay;
    }

    private AvaControl BuildClosePrompt()
    {
        var panel = new Border
        {
            Width = 336,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = AvaHorizontalAlignment.Center,
            CornerRadius = new CornerRadius(18),
            Background = Brush("#FAFCFF"),
            BorderBrush = Brush("#D8E1EC"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 28,
                OffsetY = 14,
                Color = AvaColor.FromArgb(44, 23, 38, 59)
            })
        };

        var stack = new StackPanel
        {
            Spacing = 14
        };

        stack.Children.Add(new TextBlock
        {
            Text = "退出 InputBridge？",
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });
        stack.Children.Add(new TextBlock
        {
            Text = "退出后手机输入同步会停止，最小化则会继续在托盘运行。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = Brush("#66758A")
        });

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(84)),
                new ColumnDefinition(new GridLength(8)),
                new ColumnDefinition(new GridLength(84))
            },
            Margin = new Thickness(0, 4, 0, 0)
        };

        var cancel = PrimaryButton("取消", HideClosePrompt, subtle: true);
        Grid.SetColumn(cancel, 1);
        actions.Children.Add(cancel);

        var exit = PrimaryButton("退出", ExitApplication);
        exit.Background = Brush("#D83232");
        exit.BorderBrush = Brush("#D83232");
        Grid.SetColumn(exit, 3);
        actions.Children.Add(exit);
        stack.Children.Add(actions);

        panel.Child = stack;
        return panel;
    }

    private AvaControl BuildSettingsDrawer()
    {
        var drawer = new Border
        {
            Height = 476,
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(18),
            Background = Brush("#FAFCFF"),
            BorderBrush = Brush("#D8E1EC"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 24,
                OffsetY = -8,
                Color = AvaColor.FromArgb(34, 23, 38, 59)
            })
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(54)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(58))
            }
        };

        root.Children.Add(BuildSettingsHeader());

        var form = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 2, 0, 10)
        };
        form.Children.Add(SettingsSection("连接", [
            SettingsField("局域网 IP", _settingsIpBox, BuildSettingsIpCandidates()),
            SettingsField("端口号", _settingsPortBox)
        ]));
        form.Children.Add(SettingsSection("同步", [
            SettingsField("退格限制", _settingsBackspaceBox),
            BuildAutoClearRow(),
            SettingsToggleRow(_settingsSmartDetectionBox)
        ]));

        var scroll = new ScrollViewer
        {
            Content = form,
            Margin = new Thickness(0, 0, 0, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(new GridLength(6)),
                new ColumnDefinition(new GridLength(72))
            },
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(footer, 2);

        _settingsErrorText.FontSize = 12;
        _settingsErrorText.Foreground = Brush("#C13830");
        _settingsErrorText.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_settingsErrorText);

        var cancel = SmallButton("取消", HideSettingsDrawer);
        Grid.SetColumn(cancel, 1);
        footer.Children.Add(cancel);

        var save = SmallButton("保存", SaveSettingsFromDrawer, subtle: false);
        Grid.SetColumn(save, 3);
        footer.Children.Add(save);
        root.Children.Add(footer);

        drawer.Child = root;
        return drawer;
    }

    private AvaControl BuildSettingsHeader()
    {
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38))
            }
        };

        var title = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.Children.Add(new TextBlock
        {
            Text = "设置",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });
        _settingsUrlText.FontSize = 12;
        _settingsUrlText.Foreground = Brush("#718096");
        _settingsUrlText.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Children.Add(_settingsUrlText);
        header.Children.Add(title);

        var close = ChromeButton("×", HideSettingsDrawer);
        close.Width = 36;
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        return header;
    }

    private AvaControl BuildSettingsIpCandidates()
    {
        var candidates = AppSettings.GetLocalIpCandidates();
        var panel = new StackPanel
        {
            Orientation = AvaOrientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 6, 0, 0)
        };

        foreach (var ip in candidates.Take(3))
        {
            panel.Children.Add(SmallButton(ip, () => _settingsIpBox.Text = ip));
        }

        return panel;
    }

    private AvaControl BuildAutoClearRow()
    {
        _settingsAutoClearBox.Content = "自动清空";
        _settingsAutoClearBox.VerticalAlignment = VerticalAlignment.Center;
        _settingsAutoClearBox.Foreground = Brush("#1F334D");
        StyleSettingsTextBox(_settingsAutoClearTimeBox);
        _settingsAutoClearTimeBox.Width = 74;

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(82)),
                new ColumnDefinition(new GridLength(22))
            }
        };
        grid.Children.Add(_settingsAutoClearBox);

        Grid.SetColumn(_settingsAutoClearTimeBox, 1);
        grid.Children.Add(_settingsAutoClearTimeBox);

        var unit = new TextBlock
        {
            Text = "秒",
            FontSize = 12,
            Foreground = Brush("#718096"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = AvaHorizontalAlignment.Right
        };
        Grid.SetColumn(unit, 2);
        grid.Children.Add(unit);

        return SettingsRowShell(grid);
    }

    private async Task StartServerAsync()
    {
        try
        {
            _server = new SyncServer(_settings, _input, UpdateStatus, UpdateSyncActivity);
            await _server.StartAsync();
            UpdateStatus(false, null);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _statusText.Text = $"服务启动失败：{ex.Message}";
                _statusText.Foreground = Brush("#C13830");
                _statusPill.Background = Brush("#FFF1F0");
            });
        }
    }

    private void QueueStartupWork()
    {
        Dispatcher.UIThread.Post(() => _ = InitializeAfterFirstPaintAsync(), DispatcherPriority.Background);
    }

    private async Task InitializeAfterFirstPaintAsync()
    {
        RefreshConnectionText();
        _ = Task.Run(StartServerAsync);
        InitializeInputWatcher();
        BuildTray();

        _ = UpdateQrCodeAsync();
        _ = UpdateWifiHintAsync();
        await Task.CompletedTask;
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
        RefreshConnectionText();
        _ = UpdateQrCodeAsync();
    }

    private void RefreshConnectionText()
    {
        _urlText.Text = _settings.Url;
        _settingsUrlText.Text = $"当前地址：{_settings.Url}";
    }

    private async Task UpdateQrCodeAsync()
    {
        var url = _settings.Url;
        var bytes = await Task.Run(() => CreateQrPngBytes(url));
        if (_quitting || url != _settings.Url)
        {
            return;
        }

        _qrImage.Source = new AvaBitmap(new MemoryStream(bytes));
    }

    private async Task UpdateWifiHintAsync()
    {
        var ssid = await Task.Run(GetCurrentWifiSsid);
        if (_quitting)
        {
            return;
        }

        _wifiHintText.Text = string.IsNullOrWhiteSpace(ssid)
            ? "请保持手机和电脑处于同一 Wi-Fi"
            : $"请保持手机和电脑处于同一 Wi-Fi：{ssid}";
    }

    private void InitializeInputWatcher()
    {
        if (_watcher != null || _quitting)
        {
            return;
        }

        _watcher = new GlobalInputWatcher();
        _watcher.ExternalInput += () => _server?.NotifyExternalInput();
    }

    private void UpdateStatus(bool connected, string? clientIp)
    {
        if (_quitting)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _statusText.Text = connected
                ? string.IsNullOrWhiteSpace(clientIp) ? "● 手机已连接" : $"● 手机已连接 · {clientIp}"
                : "● 等待手机连接";
            _statusText.Foreground = connected ? Brush("#128054") : Brush("#C13830");
            _statusPill.Background = connected ? Brush("#E8F8F0") : Brush("#FFF1F0");
        });
    }

    private void UpdateSyncActivity(string message)
    {
        if (_quitting)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _activityText.Text = FormatPreviewText(message);
        });
    }

    private void ShowSettingsDrawer()
    {
        _settingsIpBox.Text = _settings.Ip;
        _settingsPortBox.Text = _settings.Port.ToString();
        _settingsBackspaceBox.Text = _settings.BackspaceLimit.ToString();
        _settingsAutoClearBox.IsChecked = _settings.AutoClear;
        _settingsAutoClearTimeBox.Text = _settings.AutoClearTime.ToString();
        _settingsSmartDetectionBox.Content = "智能感知电脑端输入后重置同步状态";
        _settingsSmartDetectionBox.IsChecked = _settings.SmartDetection;
        _settingsErrorText.Text = "";
        _settingsUrlText.Text = $"当前地址：{_settings.Url}";
        _settingsOverlay.IsVisible = true;
    }

    private void HideSettingsDrawer()
    {
        _settingsOverlay.IsVisible = false;
    }

    private void SaveSettingsFromDrawer()
    {
        if (!int.TryParse(_settingsPortBox.Text, out var port) || port is < 1 or > 65535)
        {
            _settingsErrorText.Text = "端口号必须是 1-65535";
            return;
        }

        if (!int.TryParse(_settingsBackspaceBox.Text, out var backspaceLimit) || backspaceLimit < 0)
        {
            _settingsErrorText.Text = "退格限制必须是非负整数";
            return;
        }

        if (!int.TryParse(_settingsAutoClearTimeBox.Text, out var autoClearTime) || autoClearTime < 1)
        {
            _settingsErrorText.Text = "清空时间必须大于 0 秒";
            return;
        }

        var oldPort = _settings.Port;
        _settings = new AppSettings
        {
            Ip = (_settingsIpBox.Text ?? "").Trim(),
            Port = port,
            BackspaceLimit = backspaceLimit,
            AutoClear = _settingsAutoClearBox.IsChecked == true,
            AutoClearTime = autoClearTime,
            SmartDetection = _settingsSmartDetectionBox.IsChecked == true
        };
        _settings.Save();

        HideSettingsDrawer();
        if (oldPort != _settings.Port)
        {
            _ = Task.Run(RestartServerAsync);
            return;
        }

        _server?.UpdateSettings(_settings);
        RefreshConnectionInfo();
    }

    private void BuildTray()
    {
        if (_quitting || _trayDisposed)
        {
            return;
        }

        _trayIcon.ToolTipText = "InputBridge";
        _trayIcon.Icon = _trayWindowIcon;
        _trayIcon.Menu = new NativeMenu
        {
            Items =
            {
                TrayMenuItem("显示窗口", RestoreWindow),
                TrayMenuItem("退出", ConfirmAndCloseAsync)
            }
        };
        _trayIcon.IsVisible = true;
        _trayIcon.Clicked += (_, _) => RestoreWindow();
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            ShowClosePrompt();
            return;
        }

        _quitting = true;
        DisposeTray();
        DisposeWatcher();
        base.OnClosing(e);
    }

    private void ConfirmAndCloseAsync()
    {
        ShowClosePrompt();
    }

    private void ShowClosePrompt()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        _settingsOverlay.IsVisible = false;
        _closeOverlay.IsVisible = true;
    }

    private void HideClosePrompt()
    {
        _closeOverlay.IsVisible = false;
    }

    private async void ExitApplication()
    {
        if (_exitStarted)
        {
            return;
        }

        _exitStarted = true;
        _quitting = true;
        _allowClose = true;
        _closeOverlay.IsVisible = false;
        _settingsOverlay.IsVisible = false;
        Hide();

        await StopServicesForExitAsync();

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
            return;
        }

        Close();
    }

    private async Task StopServicesForExitAsync()
    {
        DisposeTray();
        DisposeWatcher();

        var server = _server;
        _server = null;
        if (server == null)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await server.StopAsync(cts.Token);
        }
        catch
        {
        }
        finally
        {
            try
            {
                server.Dispose();
            }
            catch
            {
            }
        }
    }

    private void DisposeTray()
    {
        if (_trayDisposed)
        {
            return;
        }

        _trayDisposed = true;
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }

    private void DisposeWatcher()
    {
        if (_watcherDisposed)
        {
            return;
        }

        _watcherDisposed = true;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual))
        {
            return;
        }

        if (_settingsOverlay.IsVisible)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void DragWindowFromTopArea(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual))
        {
            return;
        }

        var point = e.GetPosition(this);
        if (point.Y > 82 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private static bool IsInsideButton(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is AvaButton)
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }

    private static Border Card() => new()
    {
        CornerRadius = new CornerRadius(18),
        Background = AvaBrushes.White,
        BorderBrush = Brush("#DDE6F1"),
        BorderThickness = new Thickness(1),
        BoxShadow = new BoxShadows(new BoxShadow
        {
            Blur = 18,
            OffsetY = 8,
            Color = AvaColor.FromArgb(18, 23, 38, 59)
        })
    };

    private static AvaButton PrimaryButton(string text, Action action, bool subtle = false)
    {
        var button = new AvaButton
        {
            Content = text,
            HorizontalContentAlignment = AvaHorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 38,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8),
            FontWeight = FontWeight.SemiBold,
            Background = subtle ? Brush("#F8FAFC") : Brush("#1769E0"),
            Foreground = subtle ? Brush("#1F334D") : AvaBrushes.White,
            BorderBrush = subtle ? Brush("#D6E0EC") : Brush("#1769E0"),
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static AvaButton ChromeButton(string text, Action action)
    {
        var button = PrimaryButton(text, action, subtle: true);
        button.Width = 42;
        button.Height = 34;
        button.MinHeight = 34;
        button.Padding = new Thickness(0);
        return button;
    }

    private static AvaButton HeaderButton(string text, Action action)
    {
        var button = PrimaryButton(text, action, subtle: true);
        button.Height = 34;
        button.MinHeight = 34;
        button.Padding = new Thickness(12, 0);
        return button;
    }

    private static Border SettingsSection(string title, IEnumerable<AvaControl> children)
    {
        var stack = new StackPanel
        {
            Spacing = 10
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#536174")
        });

        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = Brush("#F7FAFE"),
            BorderBrush = Brush("#DDE6F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private static AvaControl SettingsField(string label, AvaTextBox input, AvaControl? suffix = null)
    {
        StyleSettingsTextBox(input);

        var panel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(20)),
                new RowDefinition(new GridLength(36)),
                suffix == null ? new RowDefinition(new GridLength(0)) : new RowDefinition(new GridLength(32))
            }
        };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#536174")
        });

        Grid.SetRow(input, 1);
        panel.Children.Add(input);

        if (suffix != null)
        {
            Grid.SetRow(suffix, 2);
            panel.Children.Add(suffix);
        }

        return panel;
    }

    private static Border SettingsToggleRow(AvaCheckBox checkBox)
    {
        checkBox.VerticalAlignment = VerticalAlignment.Center;
        checkBox.Foreground = Brush("#1F334D");
        return SettingsRowShell(checkBox);
    }

    private static Border SettingsRowShell(AvaControl child) => new()
    {
        CornerRadius = new CornerRadius(10),
        Background = AvaBrushes.White,
        BorderBrush = Brush("#DDE6F1"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8),
        Child = child
    };

    private static void StyleSettingsTextBox(AvaTextBox input)
    {
        input.Height = 36;
        input.CornerRadius = new CornerRadius(9);
        input.BorderBrush = Brush("#D6E0EC");
        input.Background = AvaBrushes.White;
        input.Padding = new Thickness(10, 6);
        input.FontSize = 13;
    }

    private static AvaButton SmallButton(string text, Action action, bool subtle = true)
    {
        var button = PrimaryButton(text, action, subtle);
        button.MinHeight = 34;
        button.Height = 34;
        button.Padding = new Thickness(10, 5);
        button.FontSize = 12;
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(AvaColor.Parse(hex));

    private static IBrush OverlayBrush() => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(AvaColor.FromArgb(210, 248, 251, 255), 0),
            new GradientStop(AvaColor.FromArgb(184, 232, 240, 249), 0.62),
            new GradientStop(AvaColor.FromArgb(202, 255, 255, 255), 1)
        }
    };

    private static byte[] CreateQrPngBytes(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }

    private static string FormatPreviewText(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "";
        }

        if (message == "Enter")
        {
            return "已发送 Enter";
        }

        var text = message.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "已输入换行";
        }

        const int maxLength = 18;
        return text.Length <= maxLength ? text : $"…{text[^maxLength..]}";
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

    private static string? GetCurrentWifiSsid()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var separator = trimmed.IndexOf(':');
                if (separator < 0)
                {
                    continue;
                }

                var ssid = trimmed[(separator + 1)..].Trim();
                return string.IsNullOrWhiteSpace(ssid) ? null : ssid;
            }
        }
        catch
        {
        }

        return null;
    }

    private static WindowIcon? LoadWindowIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InputBridge.Assets.app.ico");
            return stream == null ? null : new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    private static AvaBitmap LoadAppIconBitmap()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InputBridge.Assets.app.ico")
            ?? throw new InvalidOperationException("Missing embedded app icon.");
        return new AvaBitmap(stream);
    }

    private static NativeMenuItem TrayMenuItem(string text, Action action)
    {
        var item = new NativeMenuItem(text);
        item.Click += (_, _) => Dispatcher.UIThread.Post(action);
        return item;
    }
}
