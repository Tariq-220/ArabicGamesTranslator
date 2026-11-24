// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameTranslator.App.Theme;
using GameTranslator.App.Views;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;

namespace GameTranslator.App.Services;

/// <summary>
/// Geliştirme aracı: <c>GameTranslator.exe --render-screens klasör</c> bütün ekranları açık ve koyu temada PNG'ye
/// döker ve kapanır. Ekranların görünümü Windows makinesi olmadan (sürekli tümleştirmede) böyle denetlenir.
/// </summary>
public static class ScreenRenderer
{
    private static IEnumerable<FrameworkElement> LogicalElements(DependencyObject root)
    {
        if(root is FrameworkElement element)yield return element;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in LogicalElements(child))yield return item;
    }
    public static void RunArabic(Application app,string folder)
    {
        Directory.CreateDirectory(folder);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        ThemeManager.Apply(app.Resources,true);
        try {
            for(int variant=0;variant<10;variant++){
                int selected=variant%5;
                var controller=Demo(OnboardingStep.Done);
                controller.Settings=controller.Settings with{Style=controller.Settings.Style with{FontSize=22,Background=40,BottomPercent=11}};
                var window=new XboxTestWindow(controller,preview:true){Width=variant<5?960:700,Height=variant<5?850:700,Left=-10000,Top=-10000,ShowActivated=false};
                var grid=(System.Windows.Controls.Grid)window.Content;
                var tabs=grid.Children.OfType<System.Windows.Controls.TabControl>().Single();tabs.SelectedIndex=selected;
                window.Show();window.UpdateLayout();
                if(controller.Settings.Style.FontSize!=22||controller.Settings.Style.Background!=40||controller.Settings.Style.BottomPercent!=11)throw new InvalidOperationException("Saved subtitle style was overwritten on startup.");
                if(selected==1){
                    var engines=LogicalElements(grid).OfType<System.Windows.Controls.ComboBox>().Single(x=>Equals(x.Tag,"engine-choice"));
                    var credentials=LogicalElements(grid).Single(x=>Equals(x.Tag,"engine-credentials"));
                    var launcher=LogicalElements(grid).Single(x=>Equals(x.Tag,"local-launcher"));
                    engines.SelectedIndex=2;window.UpdateLayout();
                    if(credentials.Visibility!=Visibility.Collapsed||launcher.Visibility!=Visibility.Visible)throw new InvalidOperationException("Local engine credential controls are incorrect.");
                    engines.SelectedIndex=10;window.UpdateLayout();
                    if(credentials.Visibility!=Visibility.Visible||launcher.Visibility!=Visibility.Collapsed)throw new InvalidOperationException("Azure credential controls are incorrect.");
                    if(!LogicalElements(grid).Any(x=>Equals(x.Tag,"azure-region")&&x.IsVisible))throw new InvalidOperationException("Azure region is not accessible.");
                }
                foreach(var element in LogicalElements(grid).Where(x=>x.IsVisible&&(Equals(x.Tag,"capture-window")||Equals(x.Tag,"engine-choice")||Equals(x.Tag,"engine-test")))){
                    var bounds=element.TransformToAncestor(grid).TransformBounds(new Rect(element.RenderSize));
                    if(bounds.Left < -1 || bounds.Right > grid.ActualWidth+1)throw new InvalidOperationException("Horizontal clipping: "+element.Tag);
                }
                if(selected==0){
                    var toggle=LogicalElements(grid).OfType<System.Windows.Controls.CheckBox>().Single(x=>Equals(x.Tag,"desktop-overlay"));
                    if(!toggle.IsVisible||toggle.IsChecked==true||DesktopSubtitleOverlay.IsOverlayEnabled)throw new InvalidOperationException("Desktop overlay must be visible as an option and disabled for TV reading.");
                    if(!LogicalElements(grid).OfType<System.Windows.Controls.Button>().Any(x=>x.Content is string text&&text.StartsWith("معاينة المصدر")&&x.IsVisible))throw new InvalidOperationException("Source preview action is missing.");
                }
                if(tabs.Items.Count!=5)throw new InvalidOperationException("Rebuild navigation pages are incomplete.");
                var startButton=LogicalElements(grid).OfType<System.Windows.Controls.Button>().Single(x=>Equals(x.Tag,"translation-start"));var startBounds=startButton.TransformToAncestor(grid).TransformBounds(new Rect(startButton.RenderSize));if(!startButton.IsVisible||startBounds.Top<0||startBounds.Bottom>grid.ActualHeight)throw new InvalidOperationException("Start translation button is not visible");
                foreach(var tag in new[]{"screen-test","connection-status","engine-test"}){
                    var control=LogicalElements(grid).Single(x=>Equals(x.Tag,tag));var bounds=control.TransformToAncestor(grid).TransformBounds(new Rect(control.RenderSize));
                    if(!control.IsVisible||bounds.Top<0||bounds.Bottom>grid.ActualHeight)throw new InvalidOperationException("Fixed connection control is hidden: "+tag);
                }
                if(!LogicalElements(grid).OfType<System.Windows.Controls.ScrollViewer>().Any(x=>Equals(x.Tag,"setup-scroll")&&x.IsVisible&&x.VerticalScrollBarVisibility==System.Windows.Controls.ScrollBarVisibility.Visible))throw new InvalidOperationException("Setup scrollbar is hidden");
                var visual=(FrameworkElement)window;
                var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);
                bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using(var file=File.Create(Path.Combine(folder,$"arabic-{variant+1}.png")))encoder.Save(file);
                window.Close();
            }
            app.Shutdown(0);
        }catch(Exception error){File.WriteAllText(Path.Combine(folder,"errors.txt"),error.ToString());app.Shutdown(1);}
    }

    public static void Run(Application app, string folder)
    {
        Directory.CreateDirectory(folder);
        // Açılan ilk pencere kendiliğinden ana pencere sayılır; kapanınca uygulama da kapanmasın.
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var log = Path.Combine(folder, "errors.txt");
        // Bir ekranın hatası diğerlerini durdurmasın; hatalar dosyaya yazılır.
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.AppendAllText(log, $"[dispatcher] {e.Exception}\n\n");
            e.Handled = true;
        };
        foreach (var dark in new[] { false, true })
        {
            ThemeManager.Apply(app.Resources, dark);
            var suffix = dark ? "koyu" : "acik";
            foreach (var step in Enum.GetValues<OnboardingStep>().Where(s => s != OnboardingStep.Done))
            {
                var controller = Demo(step);
                Save(() => new OnboardingView(controller), 640, 720, Path.Combine(folder, $"adim-{(int)step + 1}-{step}-{suffix}.png"));
            }
            Save(() => new MainView(Demo(OnboardingStep.Done)), 640, 720, Path.Combine(folder, $"ana-{suffix}.png"));
            // Uygulamanın gerçek penceresi: XAML'ı (simge dahil) açılıştaki gibi yüklenir.
            Save(() => Detach(new ShellWindow(Demo(OnboardingStep.Done))), 640, 720, Path.Combine(folder, $"pencere-{suffix}.png"));
            Save(() => Detach(new SettingsWindow(Demo(OnboardingStep.Done))), 600, 720, Path.Combine(folder, $"ayarlar-{suffix}.png"));
            Save(() => Detach(new AboutWindow()), 460, 640, Path.Combine(folder, $"hakkinda-{suffix}.png"));
        }
        app.Shutdown();
    }

    /// <summary>Pencerenin içeriğini başka bir pencerede çizebilmek için kendi penceresinden ayırır.</summary>
    private static FrameworkElement Detach(Window window)
    {
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        return content;
    }

    private static AppController Demo(OnboardingStep step)
    {
        var controller = new AppController(new DemoServices(), new DemoStore(step == OnboardingStep.Done), new DemoSettings(), new DemoSleep(),
            Localizer.Current, action => action());
        if (step != OnboardingStep.Done) controller.Step = step;
        return controller;
    }

    private static void Save(Func<FrameworkElement> make, int width, int height, string path)
    {
        try
        {
            Render(make(), width, height, path);
        }
        catch (Exception error)
        {
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "errors.txt"), $"[{Path.GetFileName(path)}] {error}\n\n");
        }
    }

    private static void Render(FrameworkElement view, int width, int height, string path)
    {
        // Görüntüye pencere zemini de girsin (içerik saydam zemin üzerinde çizilir).
        var root = new System.Windows.Controls.Border { Child = view };
        root.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "backgroundBrush");
        root.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "textPrimaryBrush");
        var window = new Window
        {
            Content = root, Width = width, Height = height, WindowStyle = WindowStyle.None, ShowInTaskbar = false,
            Left = -10000, Top = -10000, ShowActivated = false,
        };
        window.SetResourceReference(Window.BackgroundProperty, "backgroundBrush");
        window.Show();
        window.UpdateLayout();
        // Yükleme sırasında başlayan arama vb. işlerin ekrana yansıması için kısa bekleme.
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);

        var bitmap = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        // Render the RTL parent too: rendering Content alone omits its mirroring transform.
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
        window.Close();
    }

    private sealed class DemoServices : IAppServices
    {
        public ITranslationModel Translation { get; } = new DemoTranslation();
        public Task<IReadOnlyList<ConsoleInfo>> DiscoverConsolesAsync() => Task.FromResult<IReadOnlyList<ConsoleInfo>>([new("192.168.1.20", "PS5-123", true)]);
        public Task<byte[]> AccountIdFromRedirectAsync(Uri redirect) => Task.FromResult(new byte[8]);
        public Task<RegisteredConsole> RegisterAsync(string host, byte[] accountId, uint pin) => throw new NotSupportedException();
        public Task<IReadOnlyList<LgTv>> DiscoverTvsAsync() => Task.FromResult<IReadOnlyList<LgTv>>([new("[LG] webOS TV", "192.168.1.30")]);
        public Task<TvPairing> PairTvAsync(LgTv tv, string? key, string server) => throw new NotSupportedException();
        public IStreamSession MakeStream(RegisteredConsole? console, byte[]? accountId, UserSettings? settings = null) => throw new NotSupportedException();
        public IKeeper MakeKeeper(LgTv tv, string? key, IReadOnlyDictionary<string, string> parameters, Func<bool> appConnected, Action<LgTv> onMoved) => throw new NotSupportedException();
        public string? BundledTvAppVersion => null;
        public Task<bool> IsDevModeOnAsync(string host) => Task.FromResult(false);
        public Task<string> FetchDevKeyAsync(string host, string passphrase) => throw new NotSupportedException();
        public Task<string?> InstalledTvAppVersionAsync(string host, string devKey) => throw new NotSupportedException();
        public Task InstallTvAppAsync(string host, string devKey) => throw new NotSupportedException();
        public Task<int?> RenewDevSessionAsync(string host, string devKey) => Task.FromResult<int?>(null);
        public BundledTvApp? BundledApp(TvBrand brand) => null;
        public Task<bool> IsSamsungDevModeOnAsync(string host) => Task.FromResult(false);
        public Task InstallSamsungAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent) => throw new NotSupportedException();
        public Task<bool> IsAndroidDebugOnAsync(string host) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> ScanAndroidTvsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string> InstallAndroidAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent) => throw new NotSupportedException();
        public ITextReader? MakeReader(string language) => null;
    }

    private sealed class DemoTranslation : ITranslationModel
    {
        public bool IsSupported(string source, string target) => source == "en";
        public bool IsInstalled(string source, string target) => true;
        public Task DownloadAsync(string source, string target, IProgress<double> progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public ITextTranslator Create(string source, string target) => throw new NotSupportedException();
    }

    private sealed class DemoStore(bool ready) : IAppStore
    {
        public void Save<T>(string key, T value) { }
        public T? Load<T>(string key) => !ready ? default : key switch
        {
            StoreKeys.AccountId => (T)(object)new byte[8],
            StoreKeys.Console => (T)(object)new RegisteredConsole("192.168.1.20", "PS5-123", new byte[16], new byte[16]),
            StoreKeys.Tv => (T)(object)new LgTv("[LG] webOS TV", "192.168.1.30"),
            StoreKeys.TvKey => (T)(object)"key",
            _ => default,
        };
        public void Remove(string key) { }
    }

    private sealed class DemoSettings : ISettingsStore
    {
        public UserSettings Load() => new();
        public void Save(UserSettings settings) { }
    }

    private sealed class DemoSleep : ISleepGuard
    {
        public void Begin() { }
        public void End() { }
    }
}
