using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Tests.Theming;
using ChromeBookmarksManager.Theming;
using WpfApplication = System.Windows.Application;

namespace ChromeBookmarksManager.Tests.Ui;

[Collection(WpfTestCollection.Name)]
public sealed class MessageDialogRuntimeTests
{
    [Fact]
    public void Closing_does_not_accept_a_destructive_requested_default()
    {
        WpfTestHost.Run(() =>
        {
            Resources();
            var dialog = Create(MessageBoxImage.None);
            dialog.Close();
            Assert.Equal(MessageBoxResult.Cancel, Result(dialog));
        });
    }

    [Fact]
    public void Escape_cancels_and_no_icon_long_text_fits_available_width()
    {
        WpfTestHost.Run(() =>
        {
            Resources();
            var dialog = Create(MessageBoxImage.None);
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = -15000;
            dialog.Top = -15000;
            dialog.ShowActivated = false;
            dialog.Show();
            try
            {
                dialog.UpdateLayout();
                var root = Assert.IsType<Grid>(dialog.Content);
                var body = Assert.IsType<Grid>(root.Children[0]);
                var scroll = Assert.Single(body.Children.OfType<ScrollViewer>());
                Assert.True(scroll.ActualWidth <= root.ActualWidth + 1,
                    $"Message text measured {scroll.ActualWidth} inside {root.ActualWidth}.");
                var source = PresentationSource.FromVisual(dialog)!;
                dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
                Assert.Equal(MessageBoxResult.Cancel, Result(dialog));
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Clicking_yes_returns_yes_and_body_remains_exact()
    {
        WpfTestHost.Run(() =>
        {
            Resources();
            var dialog = Create(MessageBoxImage.Warning);
            var root = Assert.IsType<Grid>(dialog.Content);
            var body = Assert.IsType<Grid>(root.Children[0]);
            var scroll = Assert.Single(body.Children.OfType<ScrollViewer>());
            Assert.Equal(Message, Assert.IsType<TextBlock>(scroll.Content).Text);
            var footer = Assert.IsType<Border>(root.Children[1]);
            var buttons = Assert.IsType<StackPanel>(footer.Child);
            var yes = buttons.Children.OfType<Button>().First();
            yes.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(MessageBoxResult.Yes, Result(dialog));
        });
    }

    private static readonly string Message = "源文件已修改。\n恢复备份：" + new string('文', 300) + "/Bookmarks.backup";

    private static AppMessageDialog Create(MessageBoxImage image) => (AppMessageDialog)Activator.CreateInstance(
        typeof(AppMessageDialog), BindingFlags.Instance | BindingFlags.NonPublic, null,
        [Message, "安全确认", MessageBoxButton.YesNoCancel, image, MessageBoxResult.Yes, null], null)!;

    private static MessageBoxResult Result(AppMessageDialog dialog) =>
        (MessageBoxResult)typeof(AppMessageDialog).GetField("_result", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;

    private static void Resources()
    {
        var app = WpfApplication.Current!;
        app.Resources.MergedDictionaries.Clear();
        ThemeService.Initialize("dark");
        LocalizationService.Initialize("zh-CN");
    }
}
