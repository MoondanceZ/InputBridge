using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaBrushes = Avalonia.Media.Brushes;
using AvaButton = Avalonia.Controls.Button;
using AvaColor = Avalonia.Media.Color;
using AvaControl = Avalonia.Controls.Control;
using AvaHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;

namespace InputBridge;

public sealed class ConfirmCloseWindow : Window
{
    public ConfirmCloseWindow()
    {
        BuildWindow();
    }

    private void BuildWindow()
    {
        Title = "退出确认";
        Width = 360;
        Height = 208;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Background = AvaBrushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Topmost = false;

        var surface = new Border
        {
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Background = Brush("#FAFCFF"),
            BorderBrush = Brush("#D8E1EC"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22),
            Child = BuildContent()
        };

        Content = surface;
    }

    private AvaControl BuildContent()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(46))
            }
        };
        root.PointerPressed += DragWindow;

        var content = new StackPanel
        {
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new TextBlock
        {
            Text = "退出 InputBridge？",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#1C2739")
        });
        content.Children.Add(new TextBlock
        {
            Text = "退出后手机输入同步会停止，最小化则会继续在托盘运行。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = Brush("#66758A")
        });
        root.Children.Add(content);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(new GridLength(12)),
                new ColumnDefinition(new GridLength(96))
            }
        };
        Grid.SetRow(actions, 1);

        var cancel = Button("取消", () => Close(false), subtle: true);
        Grid.SetColumn(cancel, 1);
        actions.Children.Add(cancel);

        var exit = Button("退出", () => Close(true));
        Grid.SetColumn(exit, 3);
        actions.Children.Add(exit);
        root.Children.Add(actions);

        return root;
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private static AvaButton Button(string text, Action action, bool subtle = false)
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
            Background = subtle ? Brush("#F8FAFC") : Brush("#D83232"),
            Foreground = subtle ? Brush("#1F334D") : AvaBrushes.White,
            BorderBrush = subtle ? Brush("#D6E0EC") : Brush("#D83232"),
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(AvaColor.Parse(hex));
}
