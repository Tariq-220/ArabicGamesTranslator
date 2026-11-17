// SPDX-License-Identifier: AGPL-3.0-only
// Arabic console translation prototype, derived from Sefflex/GameTranslator.
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameTranslator.App.Services;
using GameTranslator.Core;
using GameTranslator.Shell;
using GameTranslator.Translation;
using InputBox = System.Windows.Controls.TextBox;

namespace GameTranslator.App.Views;

public sealed class XboxTestWindow : Window
{
    private readonly AppController controller;
    private readonly System.Threading.CancellationTokenSource updateCancel=new();
    private readonly TextBlock updateLabel=Label("جارٍ فحص قناة التحديث…");
    private readonly Button updateButton=new(){Content="فحص التحديث",MinHeight=48,Margin=new Thickness(4)};
    private string? readyInstaller;
    private bool checkingUpdate;
    private System.Threading.CancellationTokenSource? activeUpdateDownload;

    private readonly ComparisonLog comparison = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArabicGames", "Comparison", "comparison.jsonl"));
    private readonly CheckBox recordComparison = new() { Content="تسجيل المقارنة محليًا — الإنجليزي والعربي والمحرك والأوقات فقط", IsChecked=true, Foreground=Brushes.White, Margin=new Thickness(4) };
    private string comparisonSession=Guid.NewGuid().ToString("N"), comparisonEngine="", comparisonGame="";
    private bool comparisonLatest;
    private readonly TextBlock logState=Label("السجل مفعّل محليًا؛ لا يُرسل تلقائيًا. المقارنة قد تشمل نتائج الذاكرة المؤقتة.");
    private readonly ComboBox windows = new() { MinWidth = 400, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(34,50,71)), FlowDirection = FlowDirection.LeftToRight };
    private readonly TextBlock status = Label("جاهز — افتح جلسة Xbox Remote Play أولًا.");
    private readonly TextBlock source = Label("النص الإنجليزي سيظهر هنا.");
    private readonly TextBlock translated = Label("الترجمة العربية ستظهر هنا.", 24);
    private readonly TextBlock timing = Label("لم يُقَس زمن المعالجة بعد.");
    private readonly Button start = new() { Content = "بدء الترجمة", Foreground = Brushes.White, Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(4) };
    private readonly Slider top = new() { Minimum = 30, Maximum = 90, Value = 62, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 220 };
    private readonly Slider readLeft = new() { Minimum=0,Maximum=30,Value=10,TickFrequency=1,IsSnapToTickEnabled=true,Width=220 };
    private readonly Slider readRight = new() { Minimum=60,Maximum=100,Value=90,TickFrequency=1,IsSnapToTickEnabled=true,Width=220 };
    private readonly PasswordBox key = new() { Width = 320, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(34,50,71)), FlowDirection = FlowDirection.LeftToRight };
    private readonly ComboBox engineChoice = new() { MinWidth = 350, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(34,50,71)) };
    private readonly TextBlock connectionInfo = Label("", 16);
    private readonly Action<string, int, GameTranslator.Core.TextBox?> overlay;

    public XboxTestWindow(AppController controller)
    {
        this.controller = controller;
        start.MinHeight=48;windows.MinHeight=48;engineChoice.MinHeight=48;key.MinHeight=48;recordComparison.MinHeight=44;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative) });
        Title = "مترجم الألعاب بالعربية — لابتوب Xbox / PS5 — "+ArabicRelease.Version;
        Width = 900; Height = 820; MinWidth = 700; MinHeight = 620;
        FlowDirection = FlowDirection.RightToLeft;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 15;
        Background = new SolidColorBrush(Color.FromRgb(18, 24, 35)); Foreground = Brushes.White;
        Resources.Add(typeof(Slider),new Style(typeof(Slider)){Setters={new Setter(Slider.MinHeightProperty,44.0),new Setter(Slider.IsMoveToPointEnabledProperty,true)}});
        Resources.Add(typeof(ComboBoxItem),new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem))){Setters={new Setter(ComboBoxItem.MinHeightProperty,44.0)}});
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(Label("مترجم الألعاب بالعربية", 30));
        root.Children.Add(Button("ربط الشاشة: العنوان والخطوات",()=>MessageBox.Show(this,
            $"عنوان اللابتوب: {controller.ComputerAddress??"غير متصل"}\nالمنفذ: {controller.ServerPort}\n\nعلى TCL: OK ثم إعدادات الربط.\nفعّل مصدر كمبيوتر (بدون رمز الجوال).\nاكتب عنوان اللابتوب بدل عنوان الجوال، ثم حفظ وربط الجوال.\nاختر HDMI ثم عرض اللعبة وعودة للعب.\nمن اللابتوب اضغط اختبار الحروف العربية.\n\nالجهازان على نفس الشبكة الخاصة. لا تفتح منفذ الراوتر للإنترنت.",
            "ربط TCL باللابتوب",MessageBoxButton.OK,MessageBoxImage.Information)));
        root.Children.Add(updateLabel);root.Children.Add(updateButton);
        updateButton.Click+=async(_,_)=>{if(readyInstaller!=null)InstallReadyUpdate();else await CheckForUpdate();};
        Loaded+=async(_,_)=>await CheckForUpdate();
        root.Children.Add(Label("1 · افتح Remote Play ثم اختر نافذة اللعبة — Xbox أو PS5"));
        root.Children.Add(Button("فتح Xbox Remote Play الرسمي", () => OpenUrl("https://www.xbox.com/remoteplay")));
        root.Children.Add(Button("دليل PS5 / تنزيل PS Remote Play", () => OpenUrl("https://www.playstation.com/en-us/remote-play/")));
        root.Children.Add(Label("فعّل الترجمة الإنجليزية داخل اللعبة. أبقِ نافذة البث ظاهرة وغير مصغّرة."));
        root.Children.Add(Label("النسخة التجريبية تستخدم الإنترنت لترجمة النصوص المقروءة. لا تتصل بحساب Xbox ولا تطلب كلمة مروره."));
        var pick = Row(); pick.Children.Add(windows); pick.Children.Add(Button("تحديث النوافذ", RefreshWindows)); root.Children.Add(pick);
        root.Children.Add(Label("اختر نافذة جلسة الريموت بلاي. عند ظهور صورة سوداء جرّب التقاط الشاشة كاملة."));
        var crop = Row(); crop.Children.Add(Label("بداية منطقة القراءة من أعلى الصورة (%)")); crop.Children.Add(top); root.Children.Add(crop);
        top.Value = controller.Settings.Region.Top * 100;
        readLeft.Value=controller.Settings.Region.Left*100;readRight.Value=controller.Settings.Region.Right*100;
        var leftRow=Row();leftRow.Children.Add(Label("بداية القراءة من اليسار (%)"));leftRow.Children.Add(readLeft);root.Children.Add(leftRow);
        var rightRow=Row();rightRow.Children.Add(Label("نهاية القراءة من اليمين (%) — قلّلها لاستبعاد الأزرار"));rightRow.Children.Add(readRight);root.Children.Add(rightRow);
        var regionLabel=Label("");root.Children.Add(regionLabel);
        void ApplyCrop(){controller.Settings=controller.Settings with {Region=new CropRegion(top.Value/100,.97,readLeft.Value/100,readRight.Value/100)};regionLabel.Text=$"منطقة الحوار: أعلى {(int)top.Value}% · يسار {(int)readLeft.Value}% · يمين {(int)readRight.Value}%";}
        top.ValueChanged+=(_,_)=>ApplyCrop();readLeft.ValueChanged+=(_,_)=>ApplyCrop();readRight.ValueChanged+=(_,_)=>ApplyCrop();ApplyCrop();
        root.Children.Add(Button("الحوار فقط — استبعاد أزرار اليمين",()=>{top.Value=70;readLeft.Value=12;readRight.Value=78;ApplyCrop();status.Text="ضُيقت القراءة لوسط أسفل الفيديو. إذا انقص آخر الحوار، ارفع نهاية اليمين تدريجيًا.";}));
        root.Children.Add(Button("استعادة منطقة القراءة الواسعة",()=>{top.Value=62;readLeft.Value=10;readRight.Value=90;ApplyCrop();}));
        var latest = new CheckBox { Content="تقليل ازدحام الطلبات — قد يتخطى سطرًا قديمًا (فعّله قبل بدء الترجمة)", Margin=new Thickness(4), Foreground=Brushes.White };
        latest.Checked+=(_,_)=>controller.PreferLatestDialogue=true;
        latest.Unchecked+=(_,_)=>controller.PreferLatestDialogue=false;
        root.Children.Add(latest);
        root.Children.Add(recordComparison);root.Children.Add(logState);
        root.Children.Add(Button("فتح مجلد سجل المقارنة",()=>{var folder=System.IO.Path.GetDirectoryName(comparison.FilePath)!;System.IO.Directory.CreateDirectory(folder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",folder){UseShellExecute=true});}));
        root.Children.Add(Label("لريد ديد 2: ابدأ من 62% إلى 97%، ثم ارفع البداية لتقليل مساحة القراءة إذا كانت الترجمة واضحة."));
        var controls = Row(); controls.Children.Add(start); controls.Children.Add(Button("اختبار الحروف العربية", () => controller.SendTestSubtitle()));
        var show = new CheckBox { Content = "إظهار العربية فوق نافذة اللعب", IsChecked = true, Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Center };
        show.Checked += (_, _) => DesktopSubtitleOverlay.IsOverlayEnabled = true;
        show.Unchecked += (_, _) => DesktopSubtitleOverlay.IsOverlayEnabled = false;
        controls.Children.Add(show); root.Children.Add(controls);
        root.Children.Add(Label("2 · محرك الترجمة — لا يوجد رجوع صامت إلى مترجم أضعف عند الخطأ"));
        root.Children.Add(Label("اسم اللعبة للسياق — غيّره قبل بدء القراءة"));
        var gameName=new InputBox{Text=LiveServices.DialogueGame,MaxLength=100,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(34,50,71)),FlowDirection=FlowDirection.LeftToRight};
        gameName.TextChanged+=(_,_)=>{LiveServices.DialogueGame=gameName.Text;controller.DialogueGame=gameName.Text;};root.Children.Add(gameName);
        engineChoice.Items.Add("Gemini 3.8 Flash — سياق الحوار / low");engineChoice.Items.Add("DeepL — مفتاح الخدمة الخاص بك");engineChoice.Items.Add("QuickMT — محلي على اللابتوب");engineChoice.Items.Add("Jisr فصحى — محلي");engineChoice.Items.Add("Jisr سعودي — محلي");engineChoice.Items.Add("Gemini 3.5 Flash-Lite — تجربة خفيفة للمقارنة");engineChoice.Items.Add("HY-MT 1.8B — محلي Q4_K_M / تجريبي");engineChoice.Items.Add("Google — إنترنت بدون مفتاح / تجريبي");engineChoice.SelectedIndex=Math.Clamp(controller.ArabicEngineChoice,0,7);LiveServices.GeminiModel=engineChoice.SelectedIndex==5?"gemini-3.5-flash-lite":null;
        engineChoice.SelectionChanged+=(_,_)=>ChooseEngine();root.Children.Add(engineChoice);root.Children.Add(Label("Google بدون مفتاح يحتاج إنترنت ويرسل النص إلى Google؛ ليس Google Cloud API الرسمي. تصحيح قراءة وأسماء RDR2 مفعّل عند اختيار اسم اللعبة المطابق."));
        root.Children.Add(Button("تشغيل النموذج المحلي المختار — أول مرة تنزيل",StartLocalModel));
        var keys = Row(); keys.Children.Add(key); keys.Children.Add(Button("حفظ واختبار مفتاح المحرك المختار", SaveKey)); root.Children.Add(keys);
        root.Children.Add(Label("Gemini: بعد الحفظ تبقى الخانة فارغة لحماية المفتاح. اضغط حفظ واختبار لإعادة فحص المفتاح المحفوظ."));
        root.Children.Add(Label("3 · ربط الشاشة: فعّل مصدر كمبيوتر في تطبيق TCL وأدخل هذا العنوان. اللابتوب يبقى شغالًا؛ الجوال يمكن إقفاله."));
        connectionInfo.Text=$"عنوان الكمبيوتر: {controller.ComputerAddress ?? "غير متصل"} | المنفذ: {controller.ServerPort}";
        root.Children.Add(connectionInfo);root.Children.Add(Button("نسخ عنوان الكمبيوتر",()=>Clipboard.SetText(controller.ComputerAddress??"")));
        root.Children.Add(Button("QR لشاشة ثانية بالجوال أو التابلت",ShowSecondScreen));
        root.Children.Add(Label("4 · شكل النص على اللابتوب — إعدادات شاشة TCL تعدل من ريموت الشاشة"));
        var font=new Slider {Minimum=18,Maximum=48,Value=24,Width=240,IsSnapToTickEnabled=true,TickFrequency=1};
        var background=new Slider {Minimum=0,Maximum=100,Value=65,Width=240,IsSnapToTickEnabled=true,TickFrequency=5};
        var position=new Slider {Minimum=2,Maximum=25,Value=7,Width=240,IsSnapToTickEnabled=true,TickFrequency=1};
        TextBlock fontLabel=Label(""),backgroundLabel=Label(""),positionLabel=Label("");
        void ApplyStyle(){controller.Settings=controller.Settings with {Style=controller.Settings.Style with {FontSize=(int)font.Value,Background=(int)background.Value,BottomPercent=(int)position.Value,Placement=SubtitlePlacement.Fixed}};fontLabel.Text=$"حجم الخط: {(int)font.Value}";backgroundLabel.Text=$"الخلفية: {(int)background.Value}%";positionLabel.Text=$"ارتفاع النص عن الأسفل: {(int)position.Value}%";}
        font.ValueChanged+=(_,_)=>ApplyStyle();background.ValueChanged+=(_,_)=>ApplyStyle();position.ValueChanged+=(_,_)=>ApplyStyle();
        root.Children.Add(fontLabel);root.Children.Add(font);root.Children.Add(backgroundLabel);root.Children.Add(background);root.Children.Add(positionLabel);root.Children.Add(position);ApplyStyle();
        root.Children.Add(status); root.Children.Add(timing);
        source.FlowDirection = FlowDirection.LeftToRight;
        root.Children.Add(Card(source)); root.Children.Add(Card(translated));
        root.Children.Add(Label("القياس من وصول الإطار للبرنامج إلى تجهيز الترجمة؛ لا يشمل تأخر بث Xbox أو زمن عرض التلفزيون."));
        root.Children.Add(Label("PS5: اختر نافذة PS Remote Play أو chiaki-ng. اتصال حساب Microsoft/Sony يتم في التطبيق الرسمي؛ لا ندعي اختبار جهاز PS5 فعلي هنا."));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, PanningMode=PanningMode.VerticalOnly };
        DesktopSubtitleOverlay.IsOverlayEnabled = true;
        DesktopSubtitleOverlay.Initialize();
        overlay = (text, ms, box) => {
            DesktopSubtitleOverlay.Display(text, ms, controller.Settings.Style, box);
            if(recordComparison.IsChecked==true && controller.IsRunning && text==controller.LastTranslation && !string.IsNullOrWhiteSpace(controller.LastSource)) {
                var queued=comparison.Add(new ComparisonEntry(DateTimeOffset.UtcNow,comparisonSession,comparisonGame,comparisonEngine,controller.LastSource,text,controller.LastLineMetrics,controller.Settings.Region,comparisonLatest));
                logState.Text=comparison.Error ?? (queued?"السجل مفعّل — يحفظ السطور المعروضة وأوقاتها محليًا. لا يشمل زمن البث والتلفزيون.":"ازدحم السجل؛ لم يُحفظ هذا السطر.");
            }
        };
        controller.LiveSubtitleShown += overlay;
        controller.SubtitlesCleared += DesktopSubtitleOverlay.Clear;
        controller.PropertyChanged += Changed;
        start.Click += async (_, _) =>
        {
            if (controller.IsRunning) { controller.Stop(); return; }
            if (windows.SelectedItem is not ComboBoxItem { Tag: string target }) { status.Text = "اختر نافذة البث أولًا."; return; }
            if((engineChoice.SelectedIndex==0||engineChoice.SelectedIndex==5)&&!controller.HasGeminiKey || engineChoice.SelectedIndex==1&&!controller.HasDeepLKey){status.Text="أدخل مفتاح المحرك المختار واحفظه أولًا؛ لن نستبدله بمترجم آخر تلقائيًا.";return;}
            if(IsLocalModel){try{controller.SessionTranslator=LocalTranslator();}catch{status.Text="شغّل النموذج المحلي أولًا وانتظر Ready، ثم ابدأ القراءة.";return;}}else controller.SessionTranslator=engineChoice.SelectedIndex==7?new GoogleGtxTranslator():null;
            start.IsEnabled = false;
            try
            {
                controller.Settings = controller.Settings with
                {
                    SourceLanguage = "en", TargetLanguage = "ar", VideoSource = VideoSourceType.DesktopScreen,
                    DesktopWindowName = target, Region = new CropRegion(top.Value / 100, 0.97, readLeft.Value/100, readRight.Value/100),
                    DubbingEnabled = false, Engine = engineChoice.SelectedIndex==1 ? TranslationEngine.DeepL : TranslationEngine.Gemini
                };
                comparisonSession=Guid.NewGuid().ToString("N");comparisonEngine=engineChoice.SelectedItem?.ToString()??"unknown";comparisonGame=LiveServices.DialogueGame;comparisonLatest=controller.PreferLatestDialogue;
                await controller.StartAsync();
            }
            catch (Exception e) { controller.Stop(); status.Text = "تعذر بدء القراءة: " + e.Message; }
            finally { start.IsEnabled = true; }
        };
        Closed += (_, _) =>
        {
            updateCancel.Cancel();
            controller.LiveSubtitleShown -= overlay; controller.SubtitlesCleared -= DesktopSubtitleOverlay.Clear;
            controller.PropertyChanged -= Changed; DesktopSubtitleOverlay.Clear(); controller.Dispose(); comparison.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
        RefreshWindows();
    }
    private async Task CheckForUpdate()
    {
        if(checkingUpdate||readyInstaller!=null)return;
        if(controller.IsRunning){updateLabel.Text="فحص وتنزيل التحديث مؤجلان إلى ما بعد جلسة اللعب.";return;}
        if(string.IsNullOrWhiteSpace(ArabicRelease.Repository)){updateLabel.Text="قناة GitHub غير مرتبطة بعد.";return;}
        checkingUpdate=true;updateButton.IsEnabled=false;
        using var operation=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(updateCancel.Token);activeUpdateDownload=operation;
        try {
            updateLabel.Text="جارٍ فحص إصدارات GitHub…";
            var client=new GitHubUpdateClient(ArabicRelease.Repository);
            var release=await client.CheckAsync(Version.Parse(ArabicRelease.Version),operation.Token);
            if(release==null){updateLabel.Text="أنت على أحدث إصدار منشور — "+ArabicRelease.Version;return;}
            var folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArabicGamesLauncher","downloads",release.Version+"-"+Guid.NewGuid().ToString("N"));
            var zip=System.IO.Path.Combine(folder,"update.zip");
            updateLabel.Text="يتوفر "+release.Version+"؛ جارٍ التنزيل التلقائي…";
            await client.DownloadAsync(release,zip,new Progress<double>(value=>updateLabel.Text=$"تنزيل التحديث {release.Version}: {(int)(value*100)}%"),operation.Token);
            var extracted=System.IO.Path.Combine(folder,"package");
            await Task.Run(()=>GitHubUpdateClient.Extract(zip,extracted),operation.Token);
            readyInstaller=System.IO.Path.Combine(extracted,"Install.ps1");
            updateLabel.Text="التحديث "+release.Version+" جاهز ومتحقق منه. ثبته عندما تنتهي من اللعب.";
            updateButton.Content="تثبيت التحديث وإعادة التشغيل";
        }
        catch(OperationCanceledException){if(!updateCancel.IsCancellationRequested)updateLabel.Text="انتهت مهلة التحديث. ترجمة اللعبة مستمرة؛ أعد الفحص لاحقًا.";}
        catch { updateLabel.Text="تعذر فحص أو تنزيل تحديث موثوق. ترجمة اللعبة مستمرة؛ حاول لاحقًا."; }
        finally{activeUpdateDownload=null;checkingUpdate=false;updateButton.IsEnabled=true;}
    }
    private void InstallReadyUpdate()
    {
        if(readyInstaller==null)return;
        try {
            var process=new System.Diagnostics.ProcessStartInfo("powershell.exe"){UseShellExecute=false};
            foreach(var argument in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",readyInstaller})process.ArgumentList.Add(argument);
            System.Diagnostics.Process.Start(process);Close();
        }catch{updateLabel.Text="تعذر تشغيل المثبت؛ أعد المحاولة. النسخة الحالية لم تتغير.";}
    }
    private void ChooseEngine()
    { controller.Stop();controller.ArabicEngineChoice=engineChoice.SelectedIndex;LiveServices.GeminiModel=engineChoice.SelectedIndex==5?"gemini-3.5-flash-lite":null;controller.SessionTranslator=null;controller.Settings=controller.Settings with {Engine=engineChoice.SelectedIndex==1?TranslationEngine.DeepL:TranslationEngine.Gemini}; }
    private bool IsLocalModel => engineChoice.SelectedIndex is >=2 and <=4 or 6;
    private string LocalModel => engineChoice.SelectedIndex==6?"hy-mt": engineChoice.SelectedIndex==3?"jisr-msa":engineChoice.SelectedIndex==4?"jisr-saudi":"quickmt";
    private ITextTranslator LocalTranslator()
    {var path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".arabicgames","local-token.txt");return new LocalBridgeTranslator(LocalModel,System.IO.File.ReadAllText(path).Trim());}
    private static string LocalError(Exception error) => error switch {
        System.Net.Http.HttpRequestException {StatusCode:System.Net.HttpStatusCode.BadRequest} => "اسم النموذج لا يطابق الخادم. أغلق نافذة المحرك السابق وشغّل المحرك المختار من البرنامج.",
        System.Net.Http.HttpRequestException {StatusCode:System.Net.HttpStatusCode.Unauthorized} => "رمز الربط المحلي لا يطابق الخادم. أعد تشغيل المحرك والبرنامج تحت مستخدم Windows نفسه.",
        System.Net.Http.HttpRequestException {StatusCode:System.Net.HttpStatusCode.InternalServerError} => "المحرك متصل لكن توليد الترجمة فشل. جرّب جملة أقصر أو محركًا آخر.",
        System.Net.Http.HttpRequestException {StatusCode:System.Net.HttpStatusCode.ServiceUnavailable} => "المحرك ينفذ طلبًا سابقًا. انتظر ثم أعد الاختبار.",
        OperationCanceledException => "تجاوزت الترجمة مهلة الانتظار. انتظر انتهاء الطلب ثم جرّب؛ قد يكون النموذج أثقل على جهازك.",
        System.IO.FileNotFoundException => "ملف الربط المحلي غير موجود. شغّل المحرك أولًا وانتظر Ready.",
        _ => "تعذر الاتصال بالمحرك المحلي. اترك نافذته مفتوحة وتأكد من ظهور Ready." };
    private void StartLocalModel()
    {
        if(!IsLocalModel){status.Text="اختر QuickMT أو Jisr أو HY-MT من قائمة المحرك أولًا.";return;}
        var launcher=System.IO.Path.Combine(AppContext.BaseDirectory,"local-engine","Start-Local.cmd");
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launcher,LocalModel){UseShellExecute=true,WorkingDirectory=System.IO.Path.GetDirectoryName(launcher)!});status.Text="انتظر Ready في نافذة الخادم. أول تشغيل يحتاج Python وتنزيل النموذج. بعدها اضغط حفظ واختبار لفحص المحرك المحلي.";}
        catch{status.Text="ملفات تشغيل النموذج غير موجودة بجانب البرنامج. فك حزمة التحديث كاملة في مجلد البرنامج.";}
    }
    private async void SaveKey()
    {
        if(engineChoice.SelectedIndex==7){status.Text="جارٍ اختبار Google بدون مفتاح…";try{status.Text="Google: "+await new GoogleGtxTranslator().TranslateAsync("Keep going.");}catch{status.Text="تعذر اختبار Google. تحقق من الإنترنت أو جرّب لاحقًا.";}return;}
        if(IsLocalModel){try{var sample=await LocalTranslator().TranslateAsync("Keep going. Through the palms.");status.Text="اختبار محلي: "+sample;}catch(Exception error){status.Text=LocalError(error);}return;}
        if (string.IsNullOrWhiteSpace(key.Password) && !(engineChoice.SelectedIndex!=1 && controller.HasGeminiKey)) { status.Text = "أدخل المفتاح مرة واحدة؛ لا يوجد مفتاح محفوظ لهذا المحرك."; return; }
        status.Text = "جارٍ اختبار المفتاح؛ قد يستغرق الطلب الأول حتى 25 ثانية…";
        try
        {
            var result = engineChoice.SelectedIndex==1 ? await controller.SetDeepLKeyAsync(key.Password) : (string.IsNullOrWhiteSpace(key.Password)?await controller.TestSavedGeminiKeyAsync():await controller.SetGeminiKeyAsync(key.Password,persistUnverified:true));
            bool saved=result.State==OnlineKeyState.Valid;
            if(saved)key.Clear();
            status.Text=saved ? "المفتاح محفوظ محليًا بتشفير Windows. اختر النافذة ثم ابدأ الترجمة." : (controller.HasGeminiKey && engineChoice.SelectedIndex!=1?"المفتاح محفوظ. نتيجة الاتصال: ":"نتيجة الاختبار: ")+result.State+(engineChoice.SelectedIndex!=1?"\n"+LiveServices.LastKeyCheckDetail:"");
        }
        catch { status.Text = "تعذر الاتصال بالمحرك المختار. المفتاح ما زال في الخانة."; }
    }
    private void RefreshWindows()
    {
        var old = (windows.SelectedItem as ComboBoxItem)?.Tag as string ?? controller.Settings.DesktopWindowName;
        windows.Items.Clear();
        foreach (var title in WindowCaptureService.GetTopLevelWindowTitles())
        {
            var item = new ComboBoxItem { Content = title, Tag = "window:" + title };
            windows.Items.Add(item);
            if ((string)item.Tag == old || old is null && title.Contains("Xbox", StringComparison.OrdinalIgnoreCase)) windows.SelectedItem = item;
        }
        foreach (var monitor in WindowCaptureService.GetDisplayMonitors())
        {
            var item = new ComboBoxItem { Content = "الشاشة كاملة: " + monitor.Name, Tag = "monitor:" + monitor.Index };
            windows.Items.Add(item); if ((string)item.Tag == old) windows.SelectedItem = item;
        }
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(AppController.IsRunning)) {
            if(controller.IsRunning)activeUpdateDownload?.Cancel();
            else if(readyInstaller==null)_=CheckForUpdate();
        }
        connectionInfo.Text=$"عنوان الكمبيوتر: {controller.ComputerAddress ?? "غير متصل"} | المنفذ: {controller.ServerPort} | شاشات: {controller.TvClients}";
        start.Content = controller.IsRunning ? "إيقاف الترجمة" : "بدء الترجمة";
        source.Text = controller.LastSource;
        translated.Text = controller.LastTranslation;
        if (controller.LastLineMetrics is { } m)
            timing.Text = $"القراءة: {m.OcrMs} ms | الترجمة: {m.TranslationMs} ms | الإجمالي داخل البرنامج: {m.TotalMs} ms";
        status.Text = controller.OcrMissing ? "حزمة قراءة الإنجليزية مفقودة: ثبّت English من إعدادات لغات Windows."
            : controller.IsRunning ? $"حالة البث: {controller.SessionState} | "+(controller.TranslatorStatus switch {
                TranslatorStatus.ServiceUnreachable=>"تعذر الاتصال بخدمة الترجمة أو انتهت المهلة؛ المفتاح لم يُحذف.",
                TranslatorStatus.KeyRejected=>"خدمة الترجمة رفضت المفتاح.",
                TranslatorStatus.QuotaExceeded=>"وصلت إلى حد الطلبات أو انتهت حصة الخدمة. لا تحتاج تنزيل نموذج.",
                TranslatorStatus.ModelRejected=>"الخدمة رفضت اسم النموذج المختار. جرّب Gemini Flash-Lite.",
                TranslatorStatus.EngineFailed=>"تعذر تشغيل محرك الترجمة.",
                _=>"حالة الترجمة: "+controller.TranslatorStatus }) : "متوقف";
    }
    private static void OpenUrl(string url)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
    private void ShowSecondScreen()
    {
        var url=$"http://{controller.ComputerAddress??"127.0.0.1"}:{controller.ServerPort}/screen";
        using var generator=new QRCoder.QRCodeGenerator();using var data=generator.CreateQrCode(url,QRCoder.QRCodeGenerator.ECCLevel.M);using var code=new QRCoder.PngByteQRCode(data);
        using var stream=new System.IO.MemoryStream(code.GetGraphic(8));var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
        var content=new StackPanel{Margin=new Thickness(20)};content.Children.Add(Label("امسح من جهاز على الشبكة نفسها. اللابتوب يبقى يعمل؛ هذه صفحة تعرض النص فقط."));content.Children.Add(new Image{Source=bitmap,Width=300,Height=300});content.Children.Add(new InputBox{Text=url,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight});content.Children.Add(Label("رابط الكمبيوتر متاح على الشبكة المحلية دون رمز؛ استخدم شبكة خاصة موثوقة ولا تفتح المنفذ على الإنترنت."));
        var window=new Window{Title="شاشة ثانية",Owner=this,Width=470,Height=550,Content=content,Background=Background,Foreground=Foreground,FlowDirection=FlowDirection.RightToLeft};window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative)});window.ShowDialog();
    }
    private static TextBlock Label(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(4, 6, 4, 6), TextWrapping = TextWrapping.Wrap };
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
    private static Button Button(string text, Action action)
    {
        var b = new Button { MinHeight=48, FontSize=16, Content = text, Foreground = Brushes.White, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4) };
        b.Click += (_, _) => action(); return b;
    }
    private static Border Card(UIElement child) => new() { Child = child, Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 6), Background = new SolidColorBrush(Color.FromRgb(30, 40, 56)), CornerRadius = new CornerRadius(8) };
}
