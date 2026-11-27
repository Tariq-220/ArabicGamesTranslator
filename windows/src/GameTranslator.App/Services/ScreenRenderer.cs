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
    private static void VerifyOpaqueOcr(string folder)
    {
        var reader=WindowsOcrReader.Create("en");if(reader is null){File.WriteAllText(Path.Combine(folder,"ocr-probe.txt"),"English OCR unavailable on this runner; live recognition probe skipped.");return;}
        var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,640,120));dc.DrawText(new FormattedText("Coming home with Lucia.",System.Globalization.CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),36,Brushes.Black,1),new Point(18,24));}
        var bitmap=new RenderTargetBitmap(640,120,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var bytes=new byte[640*120*4];bitmap.CopyPixels(bytes,640*4,0);
        var opaque=reader.ReadSpatial(new Frame(bytes,640,120,640*4)).Text;var zero=(byte[])bytes.Clone();for(int i=3;i<zero.Length;i+=4)zero[i]=0;var ignored=reader.ReadSpatial(new Frame(zero,640,120,640*4)).Text;
        if(string.IsNullOrWhiteSpace(opaque)||TextMatch.Normalize(opaque)!=TextMatch.Normalize(ignored))throw new InvalidOperationException("OCR must read RGB text consistently regardless of undefined capture alpha.");
        var game=new DrawingVisual();using(var dc=game.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(53,91,158)),null,new Rect(0,0,960,130));var line=new FormattedText("Billy: Now, you couldn't pay me to go.",System.Globalization.CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),32,Brushes.White,1);line.SetForegroundBrush(Brushes.LightGreen,0,6);dc.DrawText(line,new Point(20,30));}
        var coloured=new RenderTargetBitmap(960,130,96,96,PixelFormats.Pbgra32);coloured.Render(game);var pixels=new byte[960*130*4];coloured.CopyPixels(pixels,960*4,0);var dialogue=reader.ReadLines(new Frame(pixels,960,130,960*4),true);
        if(!TextMatch.Normalize(dialogue.Text).Contains("pay me to go")||!SpeakerMerge.HasBody(dialogue))throw new InvalidOperationException("Coloured speaker must not replace the complete white dialogue body: "+dialogue.Text);
        File.WriteAllText(Path.Combine(folder,"ocr-probe.txt"),"Synthetic Windows OCR passed with opaque and zero alpha: "+opaque+"\nBlue subtitle band: "+dialogue.Text);
    }
    private sealed class EvidenceReader(Reading lines):ITextReader
    {public string Read(Frame frame)=>lines.Text;public Reading ReadSpatial(Frame frame)=>lines;}
    private sealed class EvidenceTranslator:ITextTranslator
    {public int Calls;public Task<string> TranslateAsync(string text,CancellationToken token=default){Interlocked.Increment(ref Calls);return Task.FromResult("رسالة عربية واضحة.");}}
    private static void VerifyPhoneEvidence(WindowsOcrReader reader,string folder)
    {
        var source=new CropRegion(.11182121104655093,.8759766455174428,.3502806341923062,.7751391147904854);
        foreach(var realPhone in new[]{false,true}){
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30,38,46)),null,new Rect(0,0,1280,720));
                void Text(string value,double x,double y,double size,Brush ink)=>dc.DrawText(new FormattedText(value,System.Globalization.CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,ink,1),new Point(x,y));
                if(realPhone){
                    dc.DrawRectangle(Brushes.White,null,new Rect(500,110,300,470));Text("Kenton",565,130,28,Brushes.Black);Text("Almost there!",525,290,28,Brushes.Black);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(40,103,216)),null,new Rect(610,430,175,60));Text("I'm on it.",625,443,24,Brushes.White);
                }else{
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(40,55,90)),null,new Rect(455,585,490,48));Text("Locals: Motherfucker!",470,593,27,Brushes.White);Text("+289 m",500,100,22,Brushes.White);
                }
            }
            var bitmap=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var pixels=new byte[1280*720*4];bitmap.CopyPixels(pixels,1280*4,0);var frame=new Frame(pixels,1280,720,1280*4);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,realPhone?"phone-evidence-valid.png":"phone-evidence-gameplay.png")))encoder.Save(file);
            var reading=reader.ReadSpatial(RegionCropper.Crop(frame,source));
            if(!TextMatch.Normalize(reading.Text).Contains(realPhone?"almost there":"motherfucker"))throw new InvalidOperationException("Phone evidence OCR did not read the measured source: "+reading.Text);
            long at=0;var engine=new EvidenceTranslator();using var session=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=source}],()=>new EvidenceReader(reading),engine,()=>at);
            var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);session.Caption+=c=>{if(!c.Clear&&(!realPhone||c.Panel?.Cells.Any(cell=>TextMatch.Normalize(cell.Source).Contains("almost there")&&cell.Text.Length>0)==true))shown.TrySetResult(c);};
            session.ProcessAsync(frame).GetAwaiter().GetResult();at=500;session.ProcessAsync(frame).GetAwaiter().GetResult();
            if(realPhone){var caption=shown.Task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();if(caption.Panel?.Kind!="phone"||!caption.Panel.Cells.Any(c=>TextMatch.Normalize(c.Source).Contains("almost there")&&c.Text.Length>0))throw new InvalidOperationException("The genuine phone disappeared behind the identity guard.");}
            else {Task.Delay(150).GetAwaiter().GetResult();if(shown.Task.IsCompleted||engine.Calls!=0)throw new InvalidOperationException("Recorded gameplay/HUD was sent through the phone lane.");if(session.WithoutMessageText(reading,new(source.Left,source.Top,source.Right,source.Bottom)).Lines.Count!=reading.Lines.Count)throw new InvalidOperationException("Rejected phone stole the gameplay text.");}
            File.AppendAllText(Path.Combine(folder,"ocr-probe.txt"),"\nPhone evidence "+(realPhone?"genuine phone shown":"gameplay/HUD rejected without provider call")+": "+reading.Text.Replace('\n',' '));
        }
    }
    public static void RunArabic(Application app,string folder)
    {
        Directory.CreateDirectory(folder);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        VerifyOpaqueOcr(folder);
        ThemeManager.Apply(app.Resources,true);
        try {
            if(WindowsOcrReader.Create("en") is {} phoneReader)VerifyPhoneEvidence(phoneReader,folder);
            AssertInternalPlayerSelectable();
            for(int variant=0;variant<10;variant++){
                int selected=variant%5;
                var controller=Demo(OnboardingStep.Done);
                controller.ReadingRecoveryApplied=true;controller.PrepareRebuildAreas();
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
                if(selected==2){
                    var add=LogicalElements(grid).OfType<System.Windows.Controls.Button>().Single(x=>Equals(x.Tag,"add-car-phone"));add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    if(controller.ReadingAreas.Count(a=>a.Kind==ReadingAreaKind.Phone&&a.Variant=="car")!=1)throw new InvalidOperationException("Car phone preset was duplicated or missing.");
                    var choice=LogicalElements(grid).OfType<System.Windows.Controls.ComboBox>().Single(x=>Equals(x.Tag,"reading-area-choice"));if(choice.SelectedItem?.ToString()?.Contains("جوال السيارة")!=true)throw new InvalidOperationException("Car phone selection switched to another card.");
                    window.UpdateLayout();
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
            RenderPresentation(folder);
            app.Shutdown(0);
        }catch(Exception error){File.WriteAllText(Path.Combine(folder,"errors.txt"),error.ToString());app.Shutdown(1);}
    }

    private static void RenderPresentation(string folder)
    {
        using var _unused=new System.IO.MemoryStream();
        for(int variant=0;variant<7;variant++){
            var root=new System.Windows.Controls.Grid{Width=1280,Height=720,FlowDirection=FlowDirection.LeftToRight,Background=new LinearGradientBrush(Color.FromRgb(41,52,65),Color.FromRgb(85,94,78),45)};
            if(variant==0){
                var visual=GameTranslator.App.Controls.CaptionVisual.Text("جيسون: افتح الباب، لوسيا. سنعود إلى المنزل.",new(Backdrop:"game",Accent:false),30,78);visual.HorizontalAlignment=HorizontalAlignment.Center;visual.VerticalAlignment=VerticalAlignment.Bottom;visual.Margin=new Thickness(0,0,0,65);visual.MaxWidth=980;root.Children.Add(visual);
                visual.Measure(new Size(980,double.PositiveInfinity));if(visual.DesiredSize.Width>=980||visual.DesiredSize.Height-visual.Margin.Top-visual.Margin.Bottom>110)throw new InvalidOperationException("Dialogue background must hug its text.");
            }else if(variant is 1 or 3 or 5){
                var cells=new List<PanelCell>{new("Kenton","كينتون",new(.25,.02,.75,.08),"#F1F1F4","#20232A",.04,Heading:true),new("Map Pin Unavailable","علامة الخريطة غير متاحة",new(.07,.13,.93,.21),"#FAFAFA","#20232A",.028,Bubble:true),new("Dropped off scuba to your boat man. Happy hunting!","أوصلت معدات الغوص إلى قاربك. صيدًا موفقًا!",new(.06,.31,.9,.43),"#FAFAFA","#20232A",.03,Bubble:true),new("Still can't believe you found them doubloons.","ما زلت لا أصدق أنك وجدت تلك العملات الذهبية. نحتاجك عند السفن الغارقة.",new(.06,.47,.9,.65),"#FAFAFA","#20232A",.03,Bubble:true),new("Yeah, I'm on it man.","نعم، سأهتم بالأمر.",new(.28,.72,.96,.80),"#2867D8","#FFFFFF",.03,Bubble:true),new("Got a few things to take care of right now.","لدي بعض الأمور الآن، لكنني سأبدأ البحث لاحقًا.",new(.23,.84,.96,.97),"#2867D8","#FFFFFF",.03,Bubble:true)};
                var panel=SpatialText.ReflowPhone(new PanelLayout(420,740,"phone",null,cells));if(variant==5)panel=panel with{Cells=panel.Cells.Select((c,i)=>i==2?c with{Text=""}:c).ToArray()};var translated=GameTranslator.App.Controls.CaptionVisual.Panel(panel,ReadingArea.Phone() with{Appearance=new(Font:"tajawal",Align:"right",Vertical:"top",PhoneStyle:variant is 3 or 5?"floating":"panel")});translated.Width=360;translated.Height=650;translated.HorizontalAlignment=HorizontalAlignment.Left;translated.Margin=new Thickness(310,35,0,0);root.Children.Add(translated);
                var original=GameTranslator.App.Controls.CaptionVisual.Panel(panel with{Cells=cells.Select(c=>c with{Text=c.Source}).ToArray()},ReadingArea.Phone());original.Width=360;original.Height=650;original.HorizontalAlignment=HorizontalAlignment.Right;original.Margin=new Thickness(0,35,200,0);root.Children.Add(original);
            }else if(variant is 4 or 6){
                var notification=new PanelLayout(440,190,"notification",null,[new("Andy","آندي",new(.14,.04,.95,.23),"#F3F3F3","#20232A",.11,Heading:true),new("Hey it's Andy. Just ask for me when you get here and we'll grab the clothes from my car.","مرحبًا، أنا آندي. اسأل عني عندما تصل وسنأخذ الملابس من سيارتي.",new(.06,.28,.95,.94),"#F3F3F3","#20232A",.105,Bubble:true)]);
                if(variant==6)notification=notification with{Cells=notification.Cells.Select(c=>c.Heading?c with{Text=""}:c).ToArray()};var notice=GameTranslator.App.Controls.CaptionVisual.Panel(notification,ReadingArea.Notification());notice.Width=440;notice.Height=200;notice.HorizontalAlignment=HorizontalAlignment.Center;notice.VerticalAlignment=VerticalAlignment.Center;root.Children.Add(notice);
            }else{
                var card=GameTranslator.App.Controls.CaptionVisual.Text("اتبع لوسيا إلى السيارة",new(Font:"tajawal",Backdrop:"none",Accent:true),32,0);card.HorizontalAlignment=HorizontalAlignment.Center;card.VerticalAlignment=VerticalAlignment.Bottom;card.Margin=new Thickness(0,0,0,65);root.Children.Add(card);
            }
            root.Measure(new Size(1280,720));root.Arrange(new Rect(0,0,1280,720));root.UpdateLayout();var image=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);image.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(folder,$"presentation-{variant+1}.png"));png.Save(file);
        }
    }

    private static void AssertInternalPlayerSelectable()
    {
        var player=new Window{Title=XboxPlayerWindow.PlayerTitle,Width=640,Height=360,Left=-10000,Top=-10000,ShowActivated=false};
        player.Show();var handle=new System.Windows.Interop.WindowInteropHelper(player).Handle;
        try{
            if(WindowCaptureService.GetTopLevelWindowTitles().Contains(player.Title))throw new InvalidOperationException("Unregistered app window leaked into capture sources.");
            WindowCaptureService.RegisterInternalPlayer(handle);
            if(!WindowCaptureService.GetTopLevelWindowTitles().Contains(player.Title)||WindowCaptureService.FindWindowBySubstring(player.Title)!=handle)
                throw new InvalidOperationException("Internal Xbox player is not selectable as a capture source.");
        }finally{WindowCaptureService.UnregisterInternalPlayer(handle);player.Close();}
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

    private sealed class DemoStore : IAppStore
    {
        private readonly Dictionary<string,object?> values=new();
        public DemoStore(bool ready){if(!ready)return;values[StoreKeys.AccountId]=new byte[8];values[StoreKeys.Console]=new RegisteredConsole("192.168.1.20","PS5-123",new byte[16],new byte[16]);values[StoreKeys.Tv]=new LgTv("[LG] webOS TV","192.168.1.30");values[StoreKeys.TvKey]="key";}
        public void Save<T>(string key,T value)=>values[key]=value;
        public T? Load<T>(string key)=>values.TryGetValue(key,out var value)?(T?)value:default;
        public void Remove(string key)=>values.Remove(key);
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
