using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaBrushes = Avalonia.Media.Brushes;
using AvaColor = Avalonia.Media.Color;
using AvaHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using AvaOrientation = Avalonia.Layout.Orientation;
using AvaButton = Avalonia.Controls.Button;
using AvaCheckBox = Avalonia.Controls.CheckBox;
using AvaControl = Avalonia.Controls.Control;
using AvaTextBox = Avalonia.Controls.TextBox;

namespace InputBridge;

public sealed class SettingsWindow : Window
{
    private readonly AvaTextBox _ipBox = new();
    private readonly AvaTextBox _portBox = new();
    private readonly AvaTextBox _backspaceBox = new();
    private readonly AvaCheckBox _autoClearBox = new();
    private readonly AvaTextBox _autoClearTimeBox = new();
    private readonly AvaCheckBox _smartDetectionBox = new();
    private readonly TextBlock _errorText = new();
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        _settings = new AppSettings
        {
            Ip = settings.Ip,
            Port = settings.Port,
            BackspaceLimit = settings.BackspaceLimit,
            AutoClear = settings.AutoClear,
            AutoClearTime = settings.AutoClearTime,
            SmartDetection = settings.SmartDetection
        };

        BuildWindow();
    }

    private void BuildWindow()
    {
        Title = "InputBridge 设置";
        Width = 480;
        Height = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Background = AvaBrushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };

        var surface = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = Brush("#FAFCFF"),
            BorderBrush = Brush("#D8E1EC"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22),
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
                new RowDefinition(new GridLength(64)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(70))
            }
        };

        root.Children.Add(BuildTitleBar());

        var form = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };
        Grid.SetRow(form, 1);

        _ipBox.Text = _settings.Ip;
        _ipBox.PlaceholderText = "留空自动选择局域网 IP";

        _portBox.Text = _settings.Port.ToString();
        form.Children.Add(Section("连接", [
            Field("局域网 IP", _ipBox, BuildIpCandidates()),
            Field("端口号", _portBox)
        ]));

        _backspaceBox.Text = _settings.BackspaceLimit.ToString();

        _autoClearBox.Content = "自动清空手机输入框";
        _autoClearBox.IsChecked = _settings.AutoClear;

        _autoClearTimeBox.Text = _settings.AutoClearTime.ToString();

        _smartDetectionBox.Content = "智能感知电脑端输入后重置同步状态";
        _smartDetectionBox.IsChecked = _settings.SmartDetection;
        form.Children.Add(Section("同步", [
            Field("退格限制", _backspaceBox),
            InlineAutoClear(),
            ToggleRow(_smartDetectionBox)
        ]));

        _errorText.Foreground = Brush("#C13830");
        _errorText.FontSize = 12;
        root.Children.Add(form);

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(92)),
                new ColumnDefinition(new GridLength(12)),
                new ColumnDefinition(new GridLength(108))
            },
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Grid.SetRow(footer, 2);

        _errorText.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_errorText);

        var cancel = ActionButton("取消", () => Close(null), subtle: true);
        Grid.SetColumn(cancel, 1);
        footer.Children.Add(cancel);

        var save = ActionButton("保存设置", Save);
        Grid.SetColumn(save, 3);
        footer.Children.Add(save);
        root.Children.Add(footer);

        return root;
    }

    private AvaControl BuildTitleBar()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(40))
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
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });
        title.Children.Add(new TextBlock
        {
            Text = $"当前地址：{_settings.Url}",
            FontSize = 12,
            Foreground = Brush("#718096")
        });
        grid.Children.Add(title);

        var close = ActionButton("×", () => Close(null), subtle: true);
        close.Width = 38;
        close.Height = 34;
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);

        return grid;
    }

    private static AvaControl Field(string label, AvaTextBox input, AvaControl? suffix = null)
    {
        input.Height = 38;
        input.CornerRadius = new CornerRadius(9);
        input.BorderBrush = Brush("#D6E0EC");
        input.Background = AvaBrushes.White;
        input.Padding = new Thickness(10, 7);

        var panel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(22)),
                new RowDefinition(new GridLength(38)),
                suffix == null ? new RowDefinition(new GridLength(0)) : new RowDefinition(new GridLength(34))
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

    private static Border Section(string title, IEnumerable<AvaControl> children)
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
            CornerRadius = new CornerRadius(16),
            Background = Brush("#F7FAFE"),
            BorderBrush = Brush("#DDE6F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12),
            Child = stack
        };
    }

    private AvaControl BuildIpCandidates()
    {
        var candidates = AppSettings.GetLocalIpCandidates();
        var panel = new StackPanel
        {
            Orientation = AvaOrientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0)
        };

        foreach (var ip in candidates.Take(3))
        {
            panel.Children.Add(ActionButton(ip, () => _ipBox.Text = ip, subtle: true, compact: true));
        }

        return panel;
    }

    private static Border ToggleRow(AvaCheckBox checkBox)
    {
        checkBox.VerticalAlignment = VerticalAlignment.Center;
        checkBox.Foreground = Brush("#1F334D");
        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = AvaBrushes.White,
            BorderBrush = Brush("#DDE6F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8),
            Child = checkBox
        };
    }

    private AvaControl InlineAutoClear()
    {
        _autoClearTimeBox.Width = 82;
        _autoClearTimeBox.Height = 34;
        _autoClearTimeBox.CornerRadius = new CornerRadius(9);
        _autoClearTimeBox.BorderBrush = Brush("#D6E0EC");
        _autoClearTimeBox.Background = AvaBrushes.White;
        _autoClearTimeBox.Padding = new Thickness(10, 5);
        _autoClearBox.VerticalAlignment = VerticalAlignment.Center;
        _autoClearBox.Foreground = Brush("#1F334D");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(88)),
                new ColumnDefinition(new GridLength(24))
            }
        };

        grid.Children.Add(_autoClearBox);

        Grid.SetColumn(_autoClearTimeBox, 1);
        grid.Children.Add(_autoClearTimeBox);

        var unit = new TextBlock
        {
            Text = "秒",
            FontSize = 13,
            Foreground = Brush("#718096"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = AvaHorizontalAlignment.Right
        };
        Grid.SetColumn(unit, 2);
        grid.Children.Add(unit);

        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = AvaBrushes.White,
            BorderBrush = Brush("#DDE6F1"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8),
            Child = grid
        };
    }

    private void Save()
    {
        if (!int.TryParse(_portBox.Text, out var port) || port is < 1 or > 65535)
        {
            _errorText.Text = "端口号必须是 1-65535。";
            return;
        }

        if (!int.TryParse(_backspaceBox.Text, out var backspaceLimit) || backspaceLimit < 0)
        {
            _errorText.Text = "退格限制必须是非负整数。";
            return;
        }

        if (!int.TryParse(_autoClearTimeBox.Text, out var autoClearTime) || autoClearTime < 1)
        {
            _errorText.Text = "清空时间必须大于 0 秒。";
            return;
        }

        _settings.Ip = (_ipBox.Text ?? "").Trim();
        _settings.Port = port;
        _settings.BackspaceLimit = backspaceLimit;
        _settings.AutoClear = _autoClearBox.IsChecked == true;
        _settings.AutoClearTime = autoClearTime;
        _settings.SmartDetection = _smartDetectionBox.IsChecked == true;
        Close(_settings);
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
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
        if (point.Y > 72 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

    private static AvaButton ActionButton(string text, Action action, bool subtle = false, bool compact = false)
    {
        var button = new AvaButton
        {
            Content = text,
            HorizontalContentAlignment = AvaHorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = compact ? 28 : 38,
            CornerRadius = new CornerRadius(10),
            Padding = compact ? new Thickness(10, 4) : new Thickness(14, 8),
            FontSize = compact ? 12 : 13,
            FontWeight = FontWeight.SemiBold,
            Background = subtle ? Brush("#F8FAFC") : Brush("#1769E0"),
            Foreground = subtle ? Brush("#1F334D") : AvaBrushes.White,
            BorderBrush = subtle ? Brush("#D6E0EC") : Brush("#1769E0"),
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(AvaColor.Parse(hex));
}
