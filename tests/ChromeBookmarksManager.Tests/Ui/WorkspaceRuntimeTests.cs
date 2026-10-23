using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Infrastructure;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Tests.Theming;
using ChromeBookmarksManager.Theming;
using ChromeBookmarksManager.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeBookmarksManager.Tests.Ui;

[Collection(WpfTestCollection.Name)]
public sealed class WorkspaceRuntimeTests
{
    [Fact]
    public void Workspace_rows_and_button_labels_use_the_application_palette()
    {
        WpfTestHost.Run(() =>
        {
            LoadResources("dark", "zh-CN");
            var list = new ListView { View = new GridView(), Width = 400, Height = 180 };
            ((GridView)list.View).Columns.Add(new GridViewColumn { Header = "名称", Width = 240 });
            for (var i = 0; i < 2000; i++) list.Items.Add("Bookmark " + i);
            var button = new Button { Content = "保存", Style = (Style)WpfApplication.Current.FindResource("PrimaryButtonStyle") };
            var panel = new StackPanel(); panel.Children.Add(button); panel.Children.Add(list);
            var window = new Window { Content = panel, Width = 440, Height = 260, Left = -15000, ShowActivated = false };
            window.Show(); window.UpdateLayout();
            try
            {
                var row = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(row.ActualHeight >= 36, $"Row height was {row.ActualHeight}");
                Assert.NotNull(row.Template.FindName("RowChrome", row));
                var label = FindVisual<TextBlock>(button);
                Assert.NotNull(label);
                Assert.Equal(ColorOf("AccentForegroundBrush"), Assert.IsType<SolidColorBrush>(label.Foreground).Color);
                Assert.InRange(Enumerable.Range(0, list.Items.Count).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) != null), 1, 50);
            }
            finally { window.Close(); }
        });
    }

    private static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            if (FindVisual<T>(child) is { } nested) return nested;
        }
        return null;
    }

    [Fact]
    public async Task Theme_button_preserves_document_search_selection_history_and_language()
    {
        await WpfTestHost.Run(async () =>
        {
            LoadResources("dark", "zh-CN");
            using var files = new FixtureFiles();
            var settings = new UserSettingsStore(files.SettingsPath);
            settings.SaveLanguage("en-US");
            var window = CreateWindow(settings);
            try
            {
                var vm = Assert.IsType<MainViewModel>(window.DataContext);
                await vm.LoadBookmarksAsync(files.BookmarksPath);
                var folder = vm.FolderRoots[0].Children[0];
                vm.SelectFolder(folder);
                folder.IsExpanded = true;
                await vm.AddBookmarkAsync("电影声音示例", "https://example.com/sound");
                vm.SearchText = "example";
                await vm.WaitForPendingSearchAsync();
                var list = Assert.IsType<ListView>(window.FindName("BookmarksList"));
                list.SelectedItem = vm.DisplayedItems[0];
                var document = vm.Document;
                var selection = vm.SelectedContentItems.Select(x => x.Node).ToArray();
                var selectedFolder = vm.SelectedFolder;
                var results = vm.DisplayedItems.Select(x => x.Node).ToArray();
                var undoDescription = vm.UndoDescription;
                var themeButton = Assert.IsType<Button>(window.FindName("ThemeToggleButton"));
                themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("light", ThemeService.CurrentTheme);
                Assert.Equal("light", settings.LoadTheme());
                Assert.Equal("en-US", settings.LoadLanguage());
                Assert.Same(vm, window.DataContext);
                Assert.Same(document, vm.Document);
                Assert.Same(selectedFolder, vm.SelectedFolder);
                Assert.Equal("example", vm.SearchText);
                Assert.Equal(results, vm.DisplayedItems.Select(x => x.Node));
                Assert.Equal(selection, vm.SelectedContentItems.Select(x => x.Node));
                Assert.True(folder.IsExpanded);
                Assert.True(vm.IsDirty);
                Assert.True(vm.CanUndo);
                Assert.Equal(undoDescription, vm.UndoDescription);
                Assert.True(await vm.UndoAsync());
                Assert.False(vm.IsDirty);
                Assert.True(vm.CanRedo);
            }
            finally { CloseCleanly(window); }
        });
    }

    [Theory]
    [InlineData("dark", "zh-CN", 1180, 760)]
    [InlineData("light", "en-US", 1180, 760)]
    [InlineData("dark", "en-US", 900, 560)]
    [InlineData("light", "zh-CN", 900, 560)]
    public void Runtime_layout_keeps_theme_search_save_and_navigation_reachable(string theme, string language, double width, double height)
    {
        WpfTestHost.Run(() =>
        {
            LoadResources(theme, language);
            using var files = new FixtureFiles();
            var window = CreateWindow(new UserSettingsStore(files.SettingsPath), width, height);
            try
            {
                window.UpdateLayout();
                var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                var themeButton = Assert.IsType<Button>(window.FindName("ThemeToggleButton"));
                var searchBox = Assert.IsType<TextBox>(window.FindName("SearchBox"));
                AssertReachable(themeButton, content);
                AssertReachable(searchBox, content);
                Assert.True(themeButton.IsEnabled);
                Assert.False(searchBox.IsEnabled);
                var details = Assert.IsType<Border>(window.FindName("DetailsPanel"));
                Assert.Equal(width < 1080 ? Visibility.Collapsed : Visibility.Visible, details.Visibility);
                var tree = Assert.IsType<TreeView>(window.FindName("FolderTree"));
                var list = Assert.IsType<ListView>(window.FindName("BookmarksList"));
                Assert.True(VirtualizingPanel.GetIsVirtualizing(tree));
                Assert.True(VirtualizingPanel.GetIsVirtualizing(list));
                Assert.Equal(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(list));
                Assert.Equal(SelectionMode.Extended, list.SelectionMode);
                Assert.Equal(ColorOf("AppBackgroundBrush"), Assert.IsType<SolidColorBrush>(window.Background).Color);
                var currentMenu = Assert.IsType<MenuItem>(window.FindName(theme == "light" ? "LightModeMenuItem" : "DarkModeMenuItem"));
                currentMenu.IsChecked = false;
                currentMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.True(currentMenu.IsChecked);
            }
            finally { CloseCleanly(window); }
        });
    }

    [Fact]
    public void Existing_dialog_input_survives_theme_change_and_new_dialogs_use_current_resources()
    {
        WpfTestHost.Run(() =>
        {
            LoadResources("dark", "zh-CN");
            var edit = new BookmarkEditDialog();
            edit.Configure("测试", "保留名称", "https://example.com/retained", true, true, true);
            var box = Assert.IsType<TextBox>(edit.FindName("NameBox"));
            box.Text = "未提交的中文输入";
            ThemeService.SetTheme("light");
            edit.ApplyTemplate();
            Assert.Equal("未提交的中文输入", edit.NameValue);
            Assert.Equal("https://example.com/retained", edit.UrlValue);
            Assert.Equal(ColorOf("AppBackgroundBrush"), Assert.IsType<SolidColorBrush>(edit.Background).Color);
            var batch = new BatchBookmarkImportDialog();
            Assert.Equal(ColorOf("AppBackgroundBrush"), Assert.IsType<SolidColorBrush>(batch.Background).Color);
            LocalizationService.SetLanguage("en-US");
            Assert.Equal("Light Mode", LocalizationService.GetString("ThemeLight"));
            Assert.Equal("light", ThemeService.CurrentTheme);
            edit.Close();
            batch.Close();
        });
    }

    [Fact]
    public void Appearance_choices_apply_and_remember_theme_without_changing_language()
    {
        WpfTestHost.Run(() =>
        {
            LoadResources("dark", "zh-CN");
            using var files = new FixtureFiles();
            var settings = new UserSettingsStore(files.SettingsPath);
            settings.SaveLanguage("en-US");
            var dialog = new AppearanceDialog(settings);
            Assert.IsType<RadioButton>(dialog.FindName("LightChoice")).IsChecked = true;
            Assert.Equal("light", settings.LoadTheme());
            Assert.Equal("en-US", settings.LoadLanguage());
            Assert.Equal("light", ThemeService.CurrentTheme);
            Assert.Equal(ColorOf("AppBackgroundBrush"), Assert.IsType<SolidColorBrush>(dialog.Background).Color);
            Assert.IsType<RadioButton>(dialog.FindName("DarkChoice")).IsChecked = true;
            Assert.Equal("dark", settings.LoadTheme());
            var darkChoice = Assert.IsType<RadioButton>(dialog.FindName("DarkChoice"));
            Assert.Equal(ColorOf("PrimaryTextBrush"), Assert.IsType<SolidColorBrush>(darkChoice.Foreground).Color);
            dialog.Close();
        });
    }

    [Fact]
    public async Task Render_actual_workspace_and_dialogs_in_both_modes()
    {
        await WpfTestHost.Run(async () =>
        {
            LoadResources("dark", "zh-CN");
            using var files = new FixtureFiles();
            var settings = new UserSettingsStore(files.SettingsPath);
            var window = CreateWindow(settings);
            try
            {
                var vm = Assert.IsType<MainViewModel>(window.DataContext);
                await vm.LoadBookmarksAsync(files.BookmarksPath);
                vm.SelectFolder(vm.FolderRoots[0].Children[0]);
                vm.FolderRoots[0].IsExpanded = true;
                foreach (var item in new[] { ("故事结构与人物弧光", "story"), ("镜头语言参考手册", "frames"), ("频道创作资料", "studio"), ("电影声音与配乐", "sound"), ("剪辑节奏学习笔记", "editing") })
                    await vm.AddBookmarkAsync(item.Item1, "https://example.com/" + item.Item2);
                var list = Assert.IsType<ListView>(window.FindName("BookmarksList"));
                list.SelectedIndex = 0;
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Render(window, "workspace-dark.png");
                ThemeService.SetTheme("light");
                window.UpdateLayout();
                Render(window, "workspace-light.png");
                Render(window, "workspace-light-150pct.png", 1.5);
                var appearance = new AppearanceDialog(settings);
                ShowForTest(appearance);
                appearance.UpdateLayout();
                Render(appearance, "appearance-light.png");
                ThemeService.SetTheme("dark");
                appearance.UpdateLayout();
                Render(appearance, "appearance-dark.png");
                appearance.Close();
                var edit = new BookmarkEditDialog();
                edit.Configure(LocalizationService.GetString("DialogAddBookmark"), "示例名称", "https://example.com/new", true, true, true);
                ShowForTest(edit);
                edit.UpdateLayout();
                Render(edit, "bookmark-editor-dark.png");
                edit.Close();
                var batch = new BatchBookmarkImportDialog();
                ShowForTest(batch);
                Assert.IsType<TextBox>(batch.FindName("UrlsBox")).Text = "https://example.com/cinema\nhttps://example.org/story";
                batch.UpdateLayout();
                Render(batch, "batch-dark.png");
                batch.Close();
            }
            finally { CloseCleanly(window); }
        });
    }

    private static void LoadResources(string theme, string language)
    {
        var app = WpfApplication.Current!;
        app.Resources.MergedDictionaries.Clear();
        ThemeService.Initialize(theme);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/ChromeBookmarksManager;component/Themes/Controls.xaml")
        });
        LocalizationService.Initialize(language);
    }

    private static MainWindow CreateWindow(UserSettingsStore settings, double width = 1180, double height = 760)
    {
        var window = new MainWindow(settings) { Width = width, Height = height };
        ShowForTest(window);
        return window;
    }

    private static void ShowForTest(Window window)
    {
        window.ShowActivated = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -15000;
        window.Top = -15000;
        window.Show();
        window.UpdateLayout();
    }

    private static void CloseCleanly(MainWindow window)
    {
        window.DataContext = new MainViewModel(new ChromeBookmarksReader());
        window.Close();
    }

    private static void AssertReachable(FrameworkElement element, FrameworkElement root)
    {
        Assert.True(element.ActualWidth > 0);
        var point = element.TransformToAncestor(root).Transform(new Point());
        Assert.InRange(point.X, 0, root.ActualWidth);
        Assert.True(point.X + element.ActualWidth <= root.ActualWidth + 1);
        Assert.True(point.Y + element.ActualHeight <= root.ActualHeight + 1);
    }

    private static Color ColorOf(string key) => ((SolidColorBrush)WpfApplication.Current!.FindResource(key)).Color;

    private static void Render(Window window, string name, double scale = 1)
    {
        var element = (FrameworkElement)window.Content;
        element.UpdateLayout();
        // A content root with Margin is offset in its parent. Include the margin
        // and the actual Window background so transparent areas match the live
        // client surface instead of becoming black in a PNG viewer.
        var width = element.ActualWidth + element.Margin.Left + element.Margin.Right;
        var height = element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen())
            drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var output = Path.Combine(RepositoryRoot(), "artifacts", "ui-preview");
        Directory.CreateDirectory(output);
        using var stream = File.Create(Path.Combine(output, name));
        encoder.Save(stream);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChromeBookmarksManager.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FixtureFiles : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "cbm-ui-tests", Guid.NewGuid().ToString("N"));
        public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
        public string BookmarksPath => Path.Combine(DirectoryPath, "Bookmarks.sample.json");
        public FixtureFiles()
        {
            Directory.CreateDirectory(DirectoryPath);
            var sample = File.ReadAllText(Path.Combine(RepositoryRoot(), "samples", "Bookmarks.sample.json"));
            sample = sample.Replace("Bookmarks bar", "书签栏").Replace("Other bookmarks", "其他书签").Replace("Mobile bookmarks", "移动设备书签").Replace("Examples", "电影解说").Replace("Example Documentation", "电影资料整理");
            File.WriteAllText(BookmarksPath, sample);
        }
        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, true); }
            catch (IOException) { }
        }
    }
}
