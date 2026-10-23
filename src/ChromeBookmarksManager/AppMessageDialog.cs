using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChromeBookmarksManager;

/// <summary>
/// An application-owned, theme-aware replacement for the standard WPF message box.
/// </summary>
public sealed class AppMessageDialog : Window
{
    private readonly MessageBoxButton _buttons;
    private readonly MessageBoxResult _cancellationResult;
    private readonly MessageBoxResult _defaultResult;
    private MessageBoxResult _result = MessageBoxResult.None;

    private AppMessageDialog(
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult,
        IReadOnlyDictionary<MessageBoxResult, string>? labelResources = null)
    {
        _buttons = buttons;
        _cancellationResult = GetCancellationResult(buttons);
        _defaultResult = GetDefaultResult(buttons, defaultResult);

        Title = caption;
        Width = 520;
        MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 40);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty, "WorkspaceWindowStyle");
        SetResourceReference(BackgroundProperty, "AppBackgroundBrush");
        SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        PreviewKeyDown += HandlePreviewKeyDown;

        Content = BuildContent(message, image, labelResources);
    }

    /// <summary>
    /// Shows a themed modal dialog using the same arguments and result type as
    /// <see cref="MessageBox.Show(Window, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult)"/>.
    /// </summary>
    public static MessageBoxResult Show(
        Window owner,
        string message,
        string caption,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        return ShowCore(owner, message, caption, buttons, image, defaultResult, labelResources: null);
    }

    /// <summary>
    /// Shows the standard save/discard/cancel prompt with localized action labels.
    /// </summary>
    public static MessageBoxResult ShowSaveChanges(Window owner, string message, string caption)
    {
        IReadOnlyDictionary<MessageBoxResult, string> labels = new Dictionary<MessageBoxResult, string>
        {
            [MessageBoxResult.Yes] = "MessageSave",
            [MessageBoxResult.No] = "MessageDiscard",
            [MessageBoxResult.Cancel] = "ButtonCancel"
        };

        return ShowCore(
            owner,
            message,
            caption,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.None,
            MessageBoxResult.Cancel,
            labels);
    }

    /// <summary>
    /// Returns the conservative result used when a dialog is cancelled or closed.
    /// </summary>
    public static MessageBoxResult GetCancellationResult(MessageBoxButton buttons)
    {
        return buttons switch
        {
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.OK => MessageBoxResult.OK,
            _ => MessageBoxResult.OK
        };
    }

    /// <summary>
    /// Uses an allowed requested default; otherwise falls back to the safe
    /// cancel/no/OK result for the displayed button set.
    /// </summary>
    public static MessageBoxResult GetDefaultResult(MessageBoxButton buttons, MessageBoxResult requestedDefault)
    {
        if (IsResultAvailable(buttons, requestedDefault))
        {
            return requestedDefault;
        }

        return GetCancellationResult(buttons);
    }

    private static MessageBoxResult ShowCore(
        Window owner,
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult,
        IReadOnlyDictionary<MessageBoxResult, string>? labelResources)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var dialog = new AppMessageDialog(message, caption, buttons, image, defaultResult, labelResources)
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog._result == MessageBoxResult.None
            ? dialog._cancellationResult
            : dialog._result;
    }

    private UIElement BuildContent(
        string message,
        MessageBoxImage image,
        IReadOnlyDictionary<MessageBoxResult, string>? labelResources)
    {
        var root = new Grid
        {
            Margin = new Thickness(24)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new Grid
        {
            Margin = new Thickness(0, 0, 0, 22),
            VerticalAlignment = VerticalAlignment.Top
        };
        var icon = CreateIcon(image);
        var hasIcon = icon is not null;
        body.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = hasIcon ? GridLength.Auto : new GridLength(1, GridUnitType.Star)
        });
        if (hasIcon)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        if (icon is not null)
        {
            Grid.SetColumn(icon, 0);
            body.Children.Add(icon);
        }

        var messageScrollViewer = new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(120, SystemParameters.WorkArea.Height - 230),
            Margin = hasIcon ? new Thickness(16, 0, 0, 0) : new Thickness(0)
        };
        messageScrollViewer.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        Grid.SetColumn(messageScrollViewer, GetMessageColumn(hasIcon));
        body.Children.Add(messageScrollViewer);
        Grid.SetRow(body, 0);
        root.Children.Add(body);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        foreach (var result in GetResults(buttons: _buttons))
        {
            var button = CreateButton(result, labelResources);
            button.IsDefault = result == _defaultResult;
            button.Click += (_, _) => Complete(result);
            buttonPanel.Children.Add(button);
        }

        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 16, 0, 0),
            Child = buttonPanel
        };
        footer.SetResourceReference(Border.BackgroundProperty, "SidebarBackgroundBrush");
        footer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }

    private Button CreateButton(
        MessageBoxResult result,
        IReadOnlyDictionary<MessageBoxResult, string>? labelResources)
    {
        var button = new Button
        {
            MinWidth = 92,
            Height = 36,
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(14, 0, 14, 0)
        };

        var labelKey = labelResources is not null && labelResources.TryGetValue(result, out var customLabelKey)
            ? customLabelKey
            : GetDefaultLabelResource(result);
        button.SetResourceReference(ContentControl.ContentProperty, labelKey);
        button.SetResourceReference(AutomationProperties.NameProperty, labelKey);

        var styleKey = result == MessageBoxResult.Yes || result == MessageBoxResult.OK
            ? "PrimaryButtonStyle"
            : "QuietButtonStyle";
        button.SetResourceReference(StyleProperty, styleKey);
        return button;
    }

    private static UIElement? CreateIcon(MessageBoxImage image)
    {
        var glyph = string.Empty;
        var brushKey = string.Empty;
        if (image == MessageBoxImage.Error)
        {
            glyph = "×";
            brushKey = "DangerBrush";
        }
        else if (image == MessageBoxImage.Warning)
        {
            glyph = "!";
            brushKey = "WarningBrush";
        }
        else if (image == MessageBoxImage.Information)
        {
            glyph = "i";
            brushKey = "AccentBrush";
        }
        else if (image == MessageBoxImage.Question)
        {
            glyph = "?";
            brushKey = "AccentBrush";
        }

        if (glyph.Length == 0)
        {
            return null;
        }

        var glyphText = new TextBlock
        {
            Text = glyph,
            FontSize = glyph == "i" ? 15 : 19,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        glyphText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");

        var icon = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            BorderThickness = new Thickness(2),
            Margin = new Thickness(0, 0, 0, 0),
            Child = glyphText
        };
        icon.SetResourceReference(BorderBrushProperty, brushKey);
        return icon;
    }

    private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Complete(_cancellationResult);
    }

    private void Complete(MessageBoxResult result)
    {
        _result = result;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _result = GetResultOnClose(_buttons, _result);
        base.OnClosing(e);
    }

    internal static MessageBoxResult GetResultOnClose(MessageBoxButton buttons, MessageBoxResult currentResult)
    {
        return currentResult == MessageBoxResult.None
            ? GetCancellationResult(buttons)
            : currentResult;
    }

    internal static int GetMessageColumn(bool hasIcon)
    {
        return hasIcon ? 1 : 0;
    }

    private static bool IsResultAvailable(MessageBoxButton buttons, MessageBoxResult result)
    {
        return buttons switch
        {
            MessageBoxButton.OK => result == MessageBoxResult.OK,
            MessageBoxButton.OKCancel => result is MessageBoxResult.OK or MessageBoxResult.Cancel,
            MessageBoxButton.YesNoCancel => result is MessageBoxResult.Yes or MessageBoxResult.No or MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => result is MessageBoxResult.Yes or MessageBoxResult.No,
            _ => false
        };
    }

    private static IReadOnlyList<MessageBoxResult> GetResults(MessageBoxButton buttons)
    {
        return buttons switch
        {
            MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel],
            MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],
            MessageBoxButton.YesNo => [MessageBoxResult.Yes, MessageBoxResult.No],
            _ => [MessageBoxResult.OK]
        };
    }

    private static string GetDefaultLabelResource(MessageBoxResult result)
    {
        return result switch
        {
            MessageBoxResult.Yes => "MessageYes",
            MessageBoxResult.No => "MessageNo",
            MessageBoxResult.Cancel => "ButtonCancel",
            _ => "ButtonOk"
        };
    }

}
