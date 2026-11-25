// SPDX-License-Identifier: AGPL-3.0-only
// Arabic console translation prototype, derived from Sefflex/GameTranslator.
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameTranslator.App.Services;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.Shell;
using GameTranslator.Translation;
using InputBox = System.Windows.Controls.TextBox;

namespace GameTranslator.App.Views;

public sealed class XboxTestWindow : Window
{
    private readonly AppController controller;
    private XboxPlayerWindow? xboxPlayer;
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
    private readonly ComboBox windows = new() { MinWidth = 300, Width=480,MaxWidth=480,Tag="capture-window", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(34,50,71)), FlowDirection = FlowDirection.LeftToRight };
    private readonly TextBlock connectionBar = Label("المحرك: لم يُختبر | الشاشة: غير متصلة | الترجمة: متوقفة",14);
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
    private string lastEngineCheck="لم يُختبر المحرك بعد";
    private bool engineTestPassed;
    private bool testingKey;
    private readonly ComboBox deepLMode=new(){MinWidth=350,MinHeight=48};
    private readonly Button engineHelp=new(){Content="كيف تحصل على API؟ ↗",MinHeight=48,Padding=new Thickness(14,8,14,8),Margin=new Thickness(8,0,0,0)};
    private readonly ComboBox cloudModel=new(){MinWidth=350,MinHeight=48,FlowDirection=FlowDirection.LeftToRight};
    private AiProvider CloudProvider => engineChoice.SelectedIndex==8?AiProvider.OpenAi:engineChoice.SelectedIndex==9?AiProvider.Claude:AiProvider.Gemini;
    private string? SelectedCloudModel => cloudModel.SelectedItem as string;
    private string ProviderKeyId => engineChoice.SelectedIndex==10?"arabic-cloud-key:azure":"arabic-cloud-key:"+CloudProvider.Id();
    private readonly InputBox azureRegion=new(){MinHeight=48,FlowDirection=FlowDirection.LeftToRight,MaxLength=64,Tag="azure-region"};
    private readonly StackPanel credentialPanel=new(){Tag="engine-credentials"};
    private readonly TextBlock engineHint=Label("");
    private readonly Button localLauncher=new(){Content="تنزيل / تشغيل النموذج المحلي",MinHeight=48,Margin=new Thickness(4),Tag="local-launcher"};
    private readonly Button testEngine=new(){Content="حفظ واختبار المحرك",MinHeight=48,Margin=new Thickness(4),Tag="engine-test"};
    private bool resetWindowPreference;private FrameworkElement? capturePicker;
    private bool DirectPs5=>controller.PreferPlayStation&&controller.NativePlayStationEnabled;
    private readonly TextBlock consoleHelp=Label("");private readonly TextBlock sourceInstruction=Label("");
    private readonly TextBlock connectionInfo = Label("", 16);
    private readonly Action<string, int, GameTranslator.Core.TextBox?> overlay;

    public XboxTestWindow(AppController controller, bool preview=false)
    {
        this.controller = controller;
        if(!preview)controller.PrepareDialogueReading();
        if(!controller.ReadingRecoveryApplied){controller.Settings=controller.Settings with{Region=new CropRegion(.62,.97,.12,.78)};controller.DesktopOverlayEnabled=false;controller.ReadingRecoveryApplied=true;}
        if(!preview)controller.PrepareRebuildAreas();
        start.MinHeight=56;start.MinWidth=190;start.Tag="translation-start";windows.MinHeight=48;engineChoice.MinHeight=48;key.MinHeight=48;recordComparison.MinHeight=44;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative) });
        Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/GameTranslator;component/Assets/GameTranslator.ico"));
        Title = "مترجم الألعاب بالعربية — لابتوب Xbox / PS5 — "+ArabicRelease.Version;
        Width = 1120; Height = Math.Min(820,SystemParameters.WorkArea.Height); MinWidth = 700; MinHeight = 600;
        FlowDirection = FlowDirection.RightToLeft;
        FontFamily = new FontFamily("pack://application:,,,/GameTranslator;component/Assets/Fonts/#Noto Sans Arabic"); FontSize = 15;
        Background = new SolidColorBrush(Color.FromRgb(18, 24, 35)); Foreground = Brushes.White;
        Resources.Add(typeof(Slider),new Style(typeof(Slider)){Setters={new Setter(Slider.MinHeightProperty,44.0),new Setter(Slider.IsMoveToPointEnabledProperty,true)}});
        Resources.Add(typeof(ComboBoxItem),new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem))){Setters={new Setter(ComboBoxItem.MinHeightProperty,44.0)}});
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(Label("مترجم الألعاب بالعربية", 30));
        root.Children.Add(Button("ابدأ هنا — خطوات التفعيل وحل المشاكل",ShowActivationSteps));
        root.Children.Add(Button("ربط الشاشة: العنوان والخطوات",()=>MessageBox.Show(this,
            $"عنوان اللابتوب: {controller.ComputerAddress??"غير متصل"}\nالمنفذ: {controller.ServerPort}\n\nعلى شاشة Android / Google TV: OK ثم إعدادات الربط.\nفعّل مصدر كمبيوتر (بدون رمز الجوال).\nاكتب عنوان اللابتوب بدل عنوان الجوال، ثم حفظ وربط الجوال.\nاختر HDMI ثم عرض اللعبة وعودة للعب.\nمن اللابتوب اضغط اختبار الحروف العربية.\n\nالجهازان على نفس الشبكة الخاصة. لا تفتح منفذ الراوتر للإنترنت.",
            "ربط الشاشة باللابتوب",MessageBoxButton.OK,MessageBoxImage.Information)));
        root.Children.Add(updateLabel);root.Children.Add(updateButton);
        updateButton.Click+=async(_,_)=>{if(checkingUpdate){activeUpdateDownload?.Cancel();return;}if(readyInstaller!=null)InstallReadyUpdate();else await CheckForUpdate();};
        var automaticUpdates=new CheckBox{Content="فحص وتنزيل التحديث تلقائيًا خارج اللعب",IsChecked=controller.AutoUpdateEnabled,Margin=new Thickness(4),MinHeight=40};automaticUpdates.Checked+=(_,_)=>controller.AutoUpdateEnabled=true;automaticUpdates.Unchecked+=(_,_)=>controller.AutoUpdateEnabled=false;root.Children.Add(automaticUpdates);
        if(!preview)Loaded+=async(_,_)=>{_=CommunityCorrections.RefreshAsync();if(controller.AutoUpdateEnabled)await CheckForUpdate();};
        root.Children.Add(Label("1 · مصدر اللعبة — Xbox أو PS5"));
        var consoleActions=Row();consoleActions.Children.Add(Button("Xbox — فتح البث داخل التطبيق",OpenXboxPlayer));consoleActions.Children.Add(Button("تجربة تسجيل Xbox · تحرير البث",async()=>await PauseXboxForRecording()));
        consoleActions.Children.Add(Button("اختيار نافذة بث خارجية",()=>{resetWindowPreference=true;RefreshWindows();UpdateConsoleHelp();}));
        consoleActions.Children.Add(Button("PS5 — اقتران مباشر",ConfigurePlayStation));root.Children.Add(consoleActions);
        var consoleDetails=new StackPanel();consoleDetails.Children.Add(consoleHelp);UpdateConsoleHelp();
        consoleDetails.Children.Add(Button("بديل: قراءة Sony Remote Play الخارجي",LaunchPlayStation));
        consoleDetails.Children.Add(Button("خطوات ربط PS5 الرسمية",()=>OpenUrl("https://remoteplay.dl.playstation.net/remoteplay/lang/en/ps5_win.html")));
        root.Children.Add(sourceInstruction);
        consoleDetails.Children.Add(Label("المحركات السحابية ترسل النص المقروء للمزوّد؛ اختر نافذة اللعبة لتجنّب قراءة إشعاراتك. QuickMT المحلي لا يحتاج خدمة ترجمة سحابية."));
        var pick = new StackPanel(); windows.Width=double.NaN;windows.MinWidth=0;windows.MaxWidth=double.PositiveInfinity;windows.HorizontalAlignment=HorizontalAlignment.Stretch;pick.Children.Add(windows);pick.Children.Add(Button("تحديث النوافذ", RefreshWindows)); capturePicker=pick;pick.Visibility=DirectPs5?Visibility.Collapsed:Visibility.Visible;root.Children.Add(pick);
        root.Children.Add(Button("معاينة المصدر وضبط المناطق — بدون تشغيل الترجمة",()=>{var target=(windows.SelectedItem as ComboBoxItem)?.Tag as string;if(!DirectPs5&&target is null){status.Text="اختر نافذة البث أولًا.";return;}controller.Settings=controller.Settings with{VideoSource=DirectPs5?VideoSourceType.RemotePlay:VideoSourceType.DesktopScreen,DesktopWindowName=target};try{controller.StartSourcePreview();status.Text="المعاينة تعمل. افتح مناطق القراءة؛ الترجمة لم تبدأ بعد.";}catch{status.Text="تعذر فتح المعاينة؛ تحقق من مصدر البث.";}}));
        consoleDetails.Children.Add(Label("عند ظهور صورة سوداء جرّب التقاط الشاشة كاملة. عند اللعب على التلفزيون تصغر هذه الواجهة تلقائيًا؛ أبقِ نافذة البث ظاهرة."));
        root.Children.Add(new Expander{Header="تفاصيل ربط الكونسول والخصوصية",Content=consoleDetails,Foreground=Brushes.White,Margin=new Thickness(4,8,4,8)});
        var crop = Row(); crop.Children.Add(Label("بداية منطقة القراءة من أعلى الصورة (%)")); crop.Children.Add(top); root.Children.Add(crop);
        top.Value = controller.Settings.Region.Top * 100;
        readLeft.Value=controller.Settings.Region.Left*100;readRight.Value=controller.Settings.Region.Right*100;
        var leftRow=Row();leftRow.Children.Add(Label("بداية القراءة من اليسار (%)"));leftRow.Children.Add(readLeft);root.Children.Add(leftRow);
        var rightRow=Row();rightRow.Children.Add(Label("نهاية القراءة من اليمين (%) — قلّلها لاستبعاد الأزرار"));rightRow.Children.Add(readRight);root.Children.Add(rightRow);
        var regionLabel=Label("");root.Children.Add(regionLabel);
        void ApplyCrop(){controller.Settings=controller.Settings with {Region=new CropRegion(top.Value/100,controller.Settings.Region.Bottom,readLeft.Value/100,readRight.Value/100)};regionLabel.Text=$"منطقة الحوار: أعلى {(int)top.Value}% · يسار {(int)readLeft.Value}% · يمين {(int)readRight.Value}%";}
        top.ValueChanged+=(_,_)=>ApplyCrop();readLeft.ValueChanged+=(_,_)=>ApplyCrop();readRight.ValueChanged+=(_,_)=>ApplyCrop();ApplyCrop();
        var dialogueOnly=Button("الحوار فقط — استبعاد أزرار اليمين",()=>{controller.Stop();controller.ReadingContentMode="dialogue";top.Value=70;readLeft.Value=12;readRight.Value=78;ApplyCrop();status.Text="ضُيقت القراءة للحوار. اضغط بدء الترجمة؛ إذا انقص آخر الحوار ارفع نهاية اليمين تدريجيًا.";});root.Children.Add(dialogueOnly);
        root.Children.Add(Button("استعادة منطقة القراءة الواسعة",()=>{top.Value=62;readLeft.Value=10;readRight.Value=90;ApplyCrop();}));
        var latest = new CheckBox { IsChecked=controller.PreferLatestDialogue,Content="تقليل الازدحام — تجاوز السطر القديم",MaxWidth=350,Tag="latest-only",MinHeight=44, Margin=new Thickness(4), Foreground=Brushes.White };
        latest.Checked+=(_,_)=>controller.PreferLatestDialogue=true;
        latest.Unchecked+=(_,_)=>controller.PreferLatestDialogue=false;
        root.Children.Add(latest);
        var timingOptions=new StackPanel();
        void TimingSlider(string caption,int min,int max,int current,Action<int> update){var label=Label(caption+": "+current);var slider=new Slider{Minimum=min,Maximum=max,Value=current,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(8)};slider.ValueChanged+=(_,_)=>{int n=(int)slider.Value;label.Text=caption+": "+n;update(n);};timingOptions.Children.Add(label);timingOptions.Children.Add(slider);}
        void ApplyTiming(ReadingTiming value){controller.Timing=value;DesktopCaptureStreamSession.SamplingIntervalMs=(int)Math.Round(1000d/Math.Max(value.CaptureFps,value.PreviewFps));}
        ApplyTiming(controller.Timing);
        TimingSlider("إطارات القراءة في الثانية",1,60,controller.Timing.CaptureFps,n=>ApplyTiming(controller.Timing with{CaptureFps=n}));
        TimingSlider("إطارات المعاينة في الثانية",1,60,controller.Timing.PreviewFps,n=>ApplyTiming(controller.Timing with{PreviewFps=n}));
        TimingSlider("فاصل جميع المناطق · ms",16,2000,controller.Timing.AreaReadMs??450,n=>ApplyTiming(controller.Timing with{AreaReadMs=n}));
        TimingSlider("انتظار استقرار النص · ms",0,1000,controller.Timing.SettleMs,n=>ApplyTiming(controller.Timing with{SettleMs=n}));
        TimingSlider("أقل مدة للحوار قبل استبداله · ms",0,3000,controller.Timing.MinimumDisplayMs,n=>ApplyTiming(controller.Timing with{MinimumDisplayMs=n}));
        TimingSlider("اعتبار المصدر اختفى بعد · ms",50,5000,controller.Timing.GoneMs,n=>ApplyTiming(controller.Timing with{GoneMs=n}));
        var fullPreview=new CheckBox{Content="معاينة بدقة المصدر · حتى 4K إذا وفرها المصدر",IsChecked=controller.Timing.FullResolutionPreview,Foreground=Brushes.White,MinHeight=40};fullPreview.Checked+=(_,_)=>ApplyTiming(controller.Timing with{FullResolutionPreview=true});fullPreview.Unchecked+=(_,_)=>ApplyTiming(controller.Timing with{FullResolutionPreview=false});timingOptions.Children.Add(fullPreview);
        timingOptions.Children.Add(Label("المعاينة مستقلة عن OCR. رفع المعدل يزيد الحمل؛ الأرقام حد أقصى، ولا ترفع دقة بث Remote Play نفسه. فاصل المنطقة الخاص يعمل عند إلغاء الفاصل العام."));
        timingOptions.Children.Add(Button("استخدام فواصل البطاقات الخاصة",()=>ApplyTiming(controller.Timing with{AreaReadMs=null})));
        var timingExpander=new Expander{Header="التوقيت والأداء · القراءة والمعاينة",IsExpanded=true,Content=timingOptions,Foreground=Brushes.White};
        root.Children.Add(Button("إصلاح قراءة الإنجليزية — إعدادات لغة Windows",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:regionlanguage"){UseShellExecute=true})));
        root.Children.Add(Label("هذا يغيّر فاصل قراءة الإطارات فقط، ولا يسرّع Remote Play أو محرك الترجمة. اتركه مطفأً إذا ارتفع الحمل."));
        root.Children.Add(recordComparison);root.Children.Add(logState);
        root.Children.Add(Button("تصدير السجل لإرساله هنا — JSONL",()=>ComparisonExportDialog.Save(this,comparison.FilePath,text=>logState.Text=text)));
        var sharedCorrections=new CheckBox{Content="استخدام التصحيحات المشتركة والتحسين اللغوي — أوقفه مؤقتًا للمقارنة",IsChecked=CommunityCorrections.PublicEnabled,MinHeight=44,Foreground=Brushes.White,Margin=new Thickness(4)};sharedCorrections.Checked+=(_,_)=>{CommunityCorrections.PublicEnabled=true;};sharedCorrections.Unchecked+=(_,_)=>{CommunityCorrections.PublicEnabled=false;status.Text="أُوقفت التصحيحات العامة. أوقف الترجمة ثم ابدأها لمسح نتائج الجلسة السابقة. تصحيحاتك الشخصية باقية.";};root.Children.Add(sharedCorrections);
        root.Children.Add(Button("السجل — تصحيح سطر أو الإبلاغ عن خطأ لغوي",()=>new TranslationHistoryWindow(comparison.FilePath){Owner=this}.ShowDialog()));
        root.Children.Add(Button("فتح مجلد سجل المقارنة",()=>{var folder=System.IO.Path.GetDirectoryName(comparison.FilePath)!;System.IO.Directory.CreateDirectory(folder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",folder){UseShellExecute=true});}));
        root.Children.Add(Label("لريد ديد 2: ابدأ من 62% إلى 97%، ثم ارفع البداية لتقليل مساحة القراءة إذا كانت الترجمة واضحة."));
        var controls = Row(); controls.Children.Add(start); controls.Children.Add(Button("اختبار الحروف العربية", () => controller.SendTestSubtitle()));
        var show = new CheckBox { Content = "إظهار العربية على الكمبيوتر — اتركه مطفأً عند اللعب على التلفزيون", Tag="desktop-overlay", IsChecked = controller.DesktopOverlayEnabled, Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Center };
        show.Checked += (_, _) => {controller.DesktopOverlayEnabled=true;DesktopSubtitleOverlay.IsOverlayEnabled = true;};
        show.Unchecked += (_, _) => {AreaOverlayWindow.ClearAll();controller.DesktopOverlayEnabled=false;DesktopSubtitleOverlay.IsOverlayEnabled = false;};
        root.Children.Insert(root.Children.IndexOf(pick)+1,show);
        var recover=Button("إصلاح القراءة — استعادة الحوار واستبعاد الأزرار",()=>{controller.ResetDialogueReading();show.IsChecked=false;top.Value=70;readLeft.Value=12;readRight.Value=78;ApplyCrop();source.Text="";translated.Text="";DesktopSubtitleOverlay.Clear();status.Text="استُعيدت قراءة الحوار. اختر نافذة اللعبة ثم اضغط بدء الترجمة؛ مفاتيحك ونماذجك محفوظة.";});recover.Tag="reading-recovery";
        root.Children.Insert(root.Children.IndexOf(pick)+2,recover);root.Children.Add(controls);
        root.Children.Add(Label("2 · محرك الترجمة — لا يوجد رجوع صامت إلى مترجم أضعف عند الخطأ"));
        root.Children.Add(Label("اسم اللعبة للسياق — غيّره قبل بدء القراءة"));
        LiveServices.DialogueGame=controller.DialogueGame;
        var gameName=new InputBox{Text=LiveServices.DialogueGame,MaxLength=100,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(34,50,71)),FlowDirection=FlowDirection.LeftToRight};
        gameName.TextChanged+=(_,_)=>{if(controller.IsRunning)controller.Stop();LiveServices.DialogueGame=gameName.Text;controller.DialogueGame=gameName.Text;};root.Children.Add(gameName);
        var gamePresets=Row();gamePresets.Children.Add(Button("Grand Theft Auto VI — GTA 6",()=>{controller.Stop();gameName.Text="Grand Theft Auto VI";status.Text="تم اختيار GTA 6. ابدأ الترجمة بالسياق والحزمة الصحيحين.";}));gamePresets.Children.Add(Button("Red Dead Redemption 2",()=>{controller.Stop();gameName.Text="Red Dead Redemption 2";}));root.Children.Add(gamePresets);
        engineChoice.Items.Add("Gemini — اختر نموذج Flash من القائمة أدناه");engineChoice.Items.Add("DeepL — مفتاح الخدمة الخاص بك");engineChoice.Items.Add("QuickMT — محلي على اللابتوب");engineChoice.Items.Add("Jisr فصحى — محلي");engineChoice.Items.Add("Jisr سعودي — محلي");engineChoice.Items.Add("Gemini — اختيار Flash-Lite");engineChoice.Items.Add("HY-MT 1.8B — محلي Q4_K_M / تجريبي");engineChoice.Items.Add("Google — إنترنت بدون مفتاح / تجريبي");engineChoice.Items.Add("OpenAI — مفتاح مستقل / اختباري");engineChoice.Items.Add("Claude — مفتاح مستقل / اختباري");engineChoice.Items.Add("Microsoft Azure — ترجمة سحابية / للمقارنة");engineChoice.SelectedIndex=Math.Clamp(controller.ArabicEngineChoice,0,10);engineChoice.Tag="engine-choice";LiveServices.GeminiModel=engineChoice.SelectedIndex==5?"gemini-3.5-flash-lite":null;
        engineChoice.SelectionChanged+=(_,_)=>ChooseEngine();var engineRow=new WrapPanel();engineRow.Children.Add(engineChoice);engineRow.Children.Add(engineHelp);engineHelp.Click+=(_,_)=>OpenEnginePage();root.Children.Add(engineRow);root.Children.Add(cloudModel);
        foreach(var label in new[]{"DeepL: تلقائي — إعداد الخدمة الحالي","DeepL: أسرع — latency_optimized","DeepL: تفضيل الجودة — prefer_quality_optimized"})deepLMode.Items.Add(label);
        deepLMode.SelectedIndex=Math.Clamp(controller.DeepLModeChoice,0,2);
        void ApplyDeepLMode(){LiveServices.DeepLModelType=deepLMode.SelectedIndex==1?"latency_optimized":deepLMode.SelectedIndex==2?"prefer_quality_optimized":null;}
        ApplyDeepLMode();deepLMode.SelectionChanged+=(_,_)=>{controller.Stop();controller.DeepLModeChoice=deepLMode.SelectedIndex;ApplyDeepLMode();};root.Children.Add(deepLMode);
        cloudModel.SelectionChanged+=(_,_)=>{if(SelectedCloudModel is {} model){controller.Stop();SecureStore.Default.Save("arabic-cloud-model:"+CloudProvider.Id(),model);if(CloudProvider==AiProvider.Gemini)LiveServices.GeminiModel=model;}};
        root.Children.Add(engineHint);
        localLauncher.Click+=(_,_)=>StartLocalModel();root.Children.Add(localLauncher);
        credentialPanel.Children.Add(Label("مفتاح API — اتركه فارغًا للاحتفاظ بالمفتاح المحفوظ"));credentialPanel.Children.Add(key);
        azureRegion.Text=SecureStore.Default.Load<string>("arabic-azure-region")??"";
        var regionGroup=new StackPanel{Tag="azure-region-group"};regionGroup.Children.Add(Label("منطقة مورد Azure مثل eastus — اتركها فارغة للمورد العالمي"));regionGroup.Children.Add(azureRegion);credentialPanel.Children.Add(regionGroup);
        root.Children.Add(credentialPanel);testEngine.Click+=(_,_)=>SaveKey();root.Children.Add(testEngine);RefreshCloudModels();
        
        root.Children.Add(Label("المفتاح يُحفظ بتشفير Windows؛ الخانة الفارغة لا تعني حذفه. الاختبار يرسل جملة قصيرة وقد يحتسبها المزوّد ضمن حصتك."));
        root.Children.Add(Label("3 · ربط الشاشة — من تطبيق Android / Google TV اختر اكتشاف الجوال أو الكمبيوتر وحدد هذا اللابتوب. لا تحتاج كتابة العنوان؛ اللابتوب يبقى يعمل."));
        connectionInfo.Text=$"عنوان الكمبيوتر: {controller.ComputerAddress ?? "غير متصل"} | المنفذ: {controller.ServerPort}";
        root.Children.Add(connectionInfo);root.Children.Add(Button("نسخ عنوان الكمبيوتر",()=>Clipboard.SetText(controller.ComputerAddress??"")));
        root.Children.Add(Button("اكتشاف الشاشات وربط LG webOS",ShowTvConnections));
        root.Children.Add(Button("Monitor / شاشة ثانية — QR للجوال والتابلت",ShowSecondScreen));
        root.Children.Add(Button("Monitor — إظهار العربية على شاشة الكمبيوتر",()=>{show.IsChecked=true;DesktopSubtitleOverlay.IsOverlayEnabled=true;status.Text="الترجمة ستظهر فوق نافذة اللعب على اللابتوب. للشاشة المنفصلة امسح QR من الجوال أو التابلت.";}));
        root.Children.Add(Label("4 · شكل النص على اللابتوب — إعدادات شاشة TCL تعدل من ريموت الشاشة"));
        var font=new Slider {Minimum=14,Maximum=48,Value=Math.Clamp(controller.Settings.Style.FontSize,14,48),Width=240,IsSnapToTickEnabled=true,TickFrequency=1};
        var background=new Slider {Minimum=0,Maximum=100,Value=Math.Clamp(controller.Settings.Style.Background,0,100),Width=240,IsSnapToTickEnabled=true,TickFrequency=5};
        var position=new Slider {Minimum=2,Maximum=25,Value=Math.Clamp(controller.Settings.Style.BottomPercent,2,25),Width=240,IsSnapToTickEnabled=true,TickFrequency=1};
        TextBlock fontLabel=Label(""),backgroundLabel=Label(""),positionLabel=Label("");
        void ApplyStyle(){controller.Settings=controller.Settings with {Style=controller.Settings.Style with {FontSize=(int)font.Value,Background=(int)background.Value,BottomPercent=(int)position.Value,Placement=SubtitlePlacement.Fixed}};fontLabel.Text=$"حجم الخط: {(int)font.Value}";backgroundLabel.Text=$"الخلفية: {(int)background.Value}%";positionLabel.Text=$"ارتفاع النص عن الأسفل: {(int)position.Value}%";}
        font.ValueChanged+=(_,_)=>ApplyStyle();background.ValueChanged+=(_,_)=>ApplyStyle();position.ValueChanged+=(_,_)=>ApplyStyle();
        var presets=Row();presets.Children.Add(Button("خط صغير",()=>font.Value=20));presets.Children.Add(Button("خط متوسط",()=>font.Value=26));root.Children.Add(presets);
        root.Children.Add(fontLabel);root.Children.Add(font);root.Children.Add(backgroundLabel);root.Children.Add(background);root.Children.Add(positionLabel);root.Children.Add(position);
        root.Children.Add(status); root.Children.Add(timing);
        source.FlowDirection = FlowDirection.LeftToRight;
        root.Children.Add(Card(source)); root.Children.Add(Card(translated));
        root.Children.Add(Label("القياس من وصول الإطار للبرنامج إلى تجهيز الترجمة؛ لا يشمل تأخر بث Xbox أو زمن عرض التلفزيون."));
        root.Children.Add(Label("PS5: اختر نافذة PS Remote Play أو chiaki-ng. اتصال حساب Microsoft/Sony يتم في التطبيق الرسمي؛ لا ندعي اختبار جهاز PS5 فعلي هنا."));
        // Keep the session controls visible while setup options live in four calm cards.
        root.Children.Remove(latest);root.Children.Remove(dialogueOnly);
        var quickReading=new StackPanel();quickReading.Children.Add(Label("قراءة الحوار والسرعة",17));
        var quickRow=new WrapPanel();quickRow.Children.Add(latest);quickReading.Children.Add(quickRow);quickReading.Children.Add(dialogueOnly);
        var faster=new CheckBox{Content="تحسين السرعة · تجريبي (يعيد توقيتك السابق عند إيقافه)",IsChecked=controller.SpeedBoostEnabled,Foreground=Brushes.White,MinHeight=40};faster.Checked+=(_,_)=>controller.SpeedBoostEnabled=true;faster.Unchecked+=(_,_)=>controller.SpeedBoostEnabled=false;quickReading.Children.Add(faster);
        var readingMode=new ComboBox{Tag="reading-mode",MinHeight=48,MinWidth=260,Margin=new Thickness(4)};foreach(var label in new[]{"حوار اللعبة — أسفل الصورة","قوائم وخريطة — كامل الصورة","رسائل هاتف اللعبة — يمين","رسائل هاتف اللعبة — يسار"})readingMode.Items.Add(label);var modes=new[]{"dialogue","menus","phone-right","phone-left"};readingMode.SelectedIndex=Math.Max(0,Array.IndexOf(modes,controller.ReadingContentMode));readingMode.SelectionChanged+=(_,_)=>{controller.Stop();string mode=modes[Math.Max(0,readingMode.SelectedIndex)];controller.ReadingContentMode=mode;var r=mode=="dialogue"?controller.DialogueCaptureRegion:ReadingModes.Region(mode);top.Value=r.Top*100;readLeft.Value=r.Left*100;readRight.Value=r.Right*100;ApplyCrop();status.Text="تم تغيير منطقة القراءة. افتح الحوار أو قائمة الرسائل، ثم اضغط بدء الترجمة. ارجع إلى نمط الحوار عند الانتهاء.";};quickReading.Children.Add(Label("ماذا تريد ترجمته؟",16));quickReading.Children.Add(readingMode);dialogueOnly.Click+=(_,_)=>readingMode.SelectedIndex=0;quickReading.Children.Add(Label("القوائم والرسائل تظهر مترجمة في شريط النص؛ إذا اختلف مكان الهاتف اختر الجهة أو عدّل المنطقة يدويًا.",13));
        root.Children.Insert(root.Children.IndexOf(pick)+1,Card(quickReading));
        root.Children.Insert(root.Children.IndexOf(root.Children.OfType<TextBlock>().First(x=>x.Text.StartsWith("3 ·")))+1,Button("اختبار وصول العربية للشاشة — لا يبدأ ترجمة اللعبة",()=>controller.SendTestSubtitle()));
        var minimize=new CheckBox{Content="تصغير المترجم بعد البدء — مفيد عند التقاط الشاشة كاملة",IsChecked=controller.AutoMinimizeAfterStart,Foreground=Brushes.White,MinHeight=44,Margin=new Thickness(4)};minimize.Checked+=(_,_)=>controller.AutoMinimizeAfterStart=true;minimize.Unchecked+=(_,_)=>controller.AutoMinimizeAfterStart=false;quickReading.Children.Add(minimize);
        var items=root.Children.Cast<UIElement>().ToList();
        int Section(string prefix)=>items.FindIndex(x=>x is TextBlock t&&t.Text.StartsWith(prefix,StringComparison.Ordinal));
        int sourceAt=Section("1 ·"),engineAt=Section("2 ·"),screenAt=Section("3 ·"),styleAt=Section("4 ·");
        var lastOptions=items.IndexOf(status);
        var cropAt=items.IndexOf(crop);var controlsAt=items.IndexOf(controls);
        root.Children.Clear();controls.Children.Remove(start);
        foreach(var child in items.OfType<Border>())if(child.Child==source||child.Child==translated)child.Child=null;
        var shell=new Grid{Margin=new Thickness(16)};
        shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        shell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var heading=new StackPanel{Margin=new Thickness(10)};
        heading.Children.Add(Label("ArabicGames · ترجمة الألعاب · الإصدار "+ArabicRelease.Version,20));
        updateLabel.Tag="update-status";
        connectionBar.Tag="connection-status"; heading.Children.Add(connectionBar);

        var ambient=new Border{CornerRadius=new CornerRadius(18),Padding=new Thickness(6),Margin=new Thickness(0,0,0,12),Background=new LinearGradientBrush(Color.FromRgb(34,54,77),Color.FromRgb(39,32,65),0),Child=heading};
        shell.Children.Add(ambient);
        var actions=new WrapPanel{Margin=new Thickness(0,0,0,10)};
        actions.Children.Add(start);actions.Children.Add(testEngine);
        var quickTest=Button("اختبار العربية على الشاشة",()=>{controller.SendTestSubtitle();status.Text="أُرسلت جملة اختبار للربط فقط. لترجمة اللعبة اضغط بدء الترجمة.";});quickTest.Tag="screen-test";actions.Children.Add(quickTest);
        actions.Children.Add(Button("خطوات التفعيل",ShowActivationSteps));actions.Children.Add(updateButton);
        Grid.SetRow(actions,1);shell.Children.Add(actions);
        // The old controls row owned start; it is detached before reparenting below.
        var tabs=new TabControl{FlowDirection=FlowDirection.LeftToRight,TabStripPlacement=Dock.Right,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(10)};
        void AddTab(string title,int from,int until){
            var content=new StackPanel{Margin=new Thickness(8),FlowDirection=FlowDirection.RightToLeft};
            for(int n=from;n<until;n++){
                if(n==cropAt){n=controlsAt;continue;}
                if(items[n] is FrameworkElement obsolete && Equals(obsolete.Tag,"reading-recovery"))continue;
                if(items[n] is Border oldMode && ReferenceEquals(oldMode.Child,quickReading))continue;
                if(items[n]!=controls&&items[n]!=testEngine)content.Children.Add(items[n]);
            }
            if(title.StartsWith("①"))content.Children.Add(timingExpander);
            if(title.StartsWith("②")){quickReading.Children.Remove(faster);content.Children.Add(faster);}
            var scroll=new ScrollViewer{Tag="setup-scroll",Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,PanningMode=PanningMode.VerticalOnly,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var hint=Label("↓ مرّر لعرض بقية الخيارات",13);var next=Button("بقية الخيارات ↓",()=>scroll.ScrollToVerticalOffset(scroll.VerticalOffset+Math.Max(150,scroll.ViewportHeight*.75)));void UpdateHint(){bool more=scroll.ScrollableHeight>1&&scroll.VerticalOffset<scroll.ScrollableHeight-2;hint.Text=more?"↓ توجد خيارات إضافية بالأسفل — مرّر أو اضغط السهم":"✓ كل الخيارات ظاهرة أو وصلت إلى نهايتها";next.IsEnabled=more;}scroll.ScrollChanged+=(_,_)=>UpdateHint();scroll.Loaded+=(_,_)=>UpdateHint();var hintRow=new DockPanel();DockPanel.SetDock(next,Dock.Left);hintRow.Children.Add(next);hintRow.Children.Add(hint);var area=new DockPanel();DockPanel.SetDock(hintRow,Dock.Bottom);area.Children.Add(hintRow);area.Children.Add(scroll);tabs.Items.Add(new TabItem{Header=title,MinHeight=44,Padding=new Thickness(10,8,10,8),Content=area});
        }
        AddTab("① مصدر اللعبة",sourceAt,engineAt);
        AddTab("② محرك الترجمة",engineAt,screenAt);
        tabs.Items.Add(new TabItem{Header="③ مناطق القراءة",Padding=new Thickness(12),MinHeight=52,Content=new GameTranslator.App.Controls.ReadingAreaEditor(controller){FlowDirection=FlowDirection.RightToLeft}});
        AddTab("④ الشاشة والربط",screenAt,styleAt);
        var appearance=new StackPanel{FlowDirection=FlowDirection.RightToLeft,Margin=new Thickness(12)};
        appearance.Children.Add(Label("الخط والخلفية والموقع تُعدّل لكل مربع من «مناطق القراءة». الحوار مستقل عن القوائم والرسائل.",18));
        var packStatus=Label(ArabicGamePackStore.Shared.Status,14);
        ArabicGamePackStore.Shared.Enabled=controller.ArabicPacksEnabled;
        var packEnabled=new CheckBox{Content="حزمة GTA 6 العربية الجاهزة — Sefflex (قبل المحرك)",IsChecked=controller.ArabicPacksEnabled,MinHeight=44};
        packEnabled.Checked+=(_,_)=>{controller.ArabicPacksEnabled=true;_=ArabicGamePackStore.Shared.EnsureAsync();};packEnabled.Unchecked+=(_,_)=>controller.ArabicPacksEnabled=false;
        Action packChanged=()=>Dispatcher.BeginInvoke(()=>packStatus.Text=ArabicGamePackStore.Shared.Status);
        if(!preview){ArabicGamePackStore.Shared.Changed+=packChanged;Loaded+=(_,_)=>_=ArabicGamePackStore.Shared.EnsureAsync();Closed+=(_,_)=>ArabicGamePackStore.Shared.Changed-=packChanged;}
        appearance.Children.Add(packEnabled);appearance.Children.Add(packStatus);
        appearance.Children.Add(Button("فحص / تحديث الحزمة العربية",()=>_=ArabicGamePackStore.Shared.EnsureAsync(force:true)));
        appearance.Children.Add(Label("تُنزل مباشرة من خادم GameTranslator العام (نحو 20 MB أول مرة). المطابقة التامة قبل المحرك؛ النصوص غير الموجودة تستخدم QuickMT أو DeepL. لا يعني عدد المداخل ضمان دقة كل جملة.",13));
        appearance.Children.Add(recordComparison);appearance.Children.Add(logState);
        appearance.Children.Add(Button("تصدير سجل المقارنة",()=>ComparisonExportDialog.Save(this,comparison.FilePath,text=>logState.Text=text)));
        appearance.Children.Add(Button("سجل النصوص المباشر — بحث ونسخ",()=>new LiveTranslationLogWindow(comparison.FilePath){Owner=this}.Show()));
        appearance.Children.Add(Button("التصحيحات والسجل",()=>new TranslationHistoryWindow(comparison.FilePath){Owner=this}.ShowDialog()));
        appearance.Children.Add(updateLabel);appearance.Children.Add(automaticUpdates);
        tabs.Items.Add(new TabItem{Header="⑤ السجل والتحديث",Padding=new Thickness(12),MinHeight=52,Content=new ScrollViewer{Tag="setup-scroll",Content=appearance,VerticalScrollBarVisibility=ScrollBarVisibility.Visible}});
        Grid.SetRow(tabs,2);shell.Children.Add(tabs);
        var live=new StackPanel{Margin=new Thickness(8)};
        live.Children.Add(new ScrollViewer{Content=status,MaxHeight=54,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,PanningMode=PanningMode.VerticalOnly});
        var livePreview=new StackPanel();livePreview.Children.Add(timing);livePreview.Children.Add(Card(source));livePreview.Children.Add(Card(translated));
        // Source and translated were inside cards in the former flat list.
        live.Children.Add(new Expander{Header="النص المقروء والترجمة — تفاصيل الجلسة",Content=new ScrollViewer{Content=livePreview,MaxHeight=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,PanningMode=PanningMode.VerticalOnly},IsExpanded=false,Foreground=Brushes.White});
        Grid.SetRow(live,3);shell.Children.Add(live);
        Content=shell;UpdateConnectionBar();
        DesktopSubtitleOverlay.IsOverlayEnabled = controller.DesktopOverlayEnabled;
        if(!preview)DesktopSubtitleOverlay.Initialize();
        Action<string,string> rejected=(english,arabic)=>{var dialect=ArabicOutputQuality.Accepts(english,arabic)&&ArabicRegister.NeedsRephrase(english,arabic);var reason=dialect?"dialect_output":"untranslated_output";if(recordComparison.IsChecked==true)comparison.Add(new ComparisonEntry(DateTimeOffset.UtcNow,comparisonSession,comparisonGame,comparisonEngine,english,arabic,null,controller.Settings.Region,comparisonLatest,"rejected_output",ArabicRelease.Version,DesktopCaptureStreamSession.SamplingIntervalMs,controller.PreferPlayStation?"PS5":"Xbox",WindowsOcrReader.SelectedLanguage,reason,CommunityCorrections.CurrentRevision,CommunityCorrections.PublicEnabled));logState.Text=dialect?"أعاد المحرك صياغة عامية؛ تُعاد المحاولة بالفصحى. التفاصيل في السجل.":"أعاد المحرك نصًا ناقصًا أو إنجليزيًا؛ تُعاد المحاولة. التفاصيل في السجل.";};
        Action<string,string> discarded=(english,reason)=>{if(recordComparison.IsChecked==true)comparison.Add(new ComparisonEntry(DateTimeOffset.UtcNow,comparisonSession,comparisonGame,comparisonEngine,english,"",null,controller.Settings.Region,comparisonLatest,"discarded",ArabicRelease.Version,DesktopCaptureStreamSession.SamplingIntervalMs,controller.PreferPlayStation?"PS5":"Xbox",WindowsOcrReader.SelectedLanguage,reason,CommunityCorrections.CurrentRevision,CommunityCorrections.PublicEnabled));};
        Action<AreaTrace> areaTrace=entry=>{if(recordComparison.IsChecked==true)comparison.Add(new ComparisonEntry(DateTimeOffset.UtcNow,comparisonSession,comparisonGame,comparisonEngine,entry.Source,entry.Translation,null,controller.ReadingAreas.FirstOrDefault(a=>a.Id==entry.AreaId)?.Source??new(),comparisonLatest,entry.Stage,ArabicRelease.Version,Error:entry.Error,AreaId:entry.AreaId,AreaName:entry.AreaName,ElapsedMs:entry.ElapsedMs));};
        controller.TranslationRejected+=rejected;controller.LineDiscarded+=discarded;controller.AreaTraceObserved+=areaTrace;Closed+=(_,_)=>{controller.TranslationRejected-=rejected;controller.LineDiscarded-=discarded;controller.AreaTraceObserved-=areaTrace;};
        overlay = (text, ms, box) => {
            DesktopSubtitleOverlay.Display(text, ms, controller.Settings.Style with{StackPrevious=false}, box,controller.LastCaptionLineHeight);
            if(recordComparison.IsChecked==true && controller.IsRunning && !string.IsNullOrWhiteSpace(controller.LastTranslation) && !string.IsNullOrWhiteSpace(controller.LastSource)) {
                var queued=comparison.Add(new ComparisonEntry(DateTimeOffset.UtcNow,comparisonSession,comparisonGame,comparisonEngine,controller.LastSource,controller.LastTranslation,controller.LastLineMetrics,controller.Settings.Region,comparisonLatest,controller.LastLineMetrics?.Origin==LineOrigin.Pack?"reviewed_dictionary":"engine_or_cache",ArabicRelease.Version,DesktopCaptureStreamSession.SamplingIntervalMs,controller.PreferPlayStation?"PS5":"Xbox",WindowsOcrReader.SelectedLanguage,RulesRevision:CommunityCorrections.CurrentRevision,PublicCorrections:CommunityCorrections.PublicEnabled));
                logState.Text=comparison.Error ?? (queued?"السجل مفعّل — يحفظ السطور المعروضة وأوقاتها محليًا. لا يشمل زمن البث والتلفزيون.":"ازدحم السجل؛ لم يُحفظ هذا السطر.");
            }
        };
        controller.AreasCleared+=AreaOverlayWindow.ClearAll;
        controller.AreaCaptionShown+=AreaOverlayWindow.Apply;
        controller.LiveSubtitleShown += overlay;controller.LiveCoverShown+=DesktopSubtitleOverlay.Cover;
        controller.SubtitlesCleared += DesktopSubtitleOverlay.Clear;
        controller.PropertyChanged += Changed;
        start.Click += async (_, _) =>
        {
            if (controller.IsRunning) { controller.Stop();AreaOverlayWindow.ClearAll(); return; }
            string? target=(windows.SelectedItem as ComboBoxItem)?.Tag as string;
            if(DirectPs5){if(!controller.HasAccount||controller.Console==null||!GameTranslator.RemotePlay.StreamSession.IsNativeAvailable){status.Text="أكمل اقتران PS5 المباشر وتأكد من وجود المحرك الأصلي.";return;}}
            else if(target==null){status.Text="اختر نافذة البث أولًا.";return;}
            if((engineChoice.SelectedIndex==0||engineChoice.SelectedIndex==5)&&!controller.HasGeminiKey || engineChoice.SelectedIndex==1&&!controller.HasDeepLKey){status.Text="أدخل مفتاح المحرك المختار واحفظه أولًا؛ لن نستبدله بمترجم آخر تلقائيًا.";return;}
            if(engineChoice.SelectedIndex>=8 && string.IsNullOrWhiteSpace(SecureStore.Default.Load<string>(ProviderKeyId))){status.Text="أدخل مفتاح هذا المزوّد واحفظه أولًا.";return;}
            try {
            if(IsLocalModel){try{controller.SessionTranslator=LocalTranslator();}catch{status.Text="شغّل النموذج المحلي أولًا وانتظر Ready، ثم ابدأ القراءة.";return;}}else controller.SessionTranslator=engineChoice.SelectedIndex==10?new AzureTranslator(SecureStore.Default.Load<string>(ProviderKeyId)!,azureRegion.Text):engineChoice.SelectedIndex>=8?new AiTranslator(CloudProvider,SecureStore.Default.Load<string>(ProviderKeyId)!,SelectedCloudModel,null,game:LiveServices.DialogueGame):engineChoice.SelectedIndex==7?new GoogleGtxTranslator():null;
            } catch(ArgumentException){status.Text="راجع إعداد المحرك ومنطقة Azure قبل بدء الترجمة.";return;}
            start.IsEnabled = false;
            try
            {
                controller.Settings = controller.Settings with
                {
                    SourceLanguage = "en", TargetLanguage = "ar", VideoSource = DirectPs5?VideoSourceType.RemotePlay:VideoSourceType.DesktopScreen,
                    DesktopWindowName = target, Region = controller.Settings.Region,
                    DubbingEnabled = false, Engine = engineChoice.SelectedIndex==1 ? TranslationEngine.DeepL : TranslationEngine.Gemini
                };
                comparisonSession=Guid.NewGuid().ToString("N");comparisonEngine=(engineChoice.SelectedItem?.ToString()??"unknown")+(engineChoice.SelectedIndex==1?" / "+(LiveServices.DeepLModelType??"automatic"):(cloudModel.Visibility==Visibility.Visible?" / "+SelectedCloudModel:""));comparisonGame=LiveServices.DialogueGame;comparisonLatest=controller.PreferLatestDialogue;
                await controller.StartAsync();
                if(controller.AutoMinimizeAfterStart){WindowState=WindowState.Minimized;}
            }
            catch (Exception e) { controller.Stop(); status.Text = "تعذر بدء القراءة: " + e.Message; }
            finally { start.IsEnabled = true; }
        };
        Closed += (_, _) =>
        {
            xboxPlayer?.Close();
            updateCancel.Cancel();
            controller.AreasCleared-=AreaOverlayWindow.ClearAll;
            controller.AreaCaptionShown-=AreaOverlayWindow.Apply;AreaOverlayWindow.ClearAll();
            controller.LiveCoverShown-=DesktopSubtitleOverlay.Cover;controller.LiveSubtitleShown -= overlay; controller.SubtitlesCleared -= DesktopSubtitleOverlay.Clear;
            controller.PropertyChanged -= Changed; DesktopSubtitleOverlay.Clear(); controller.Dispose(); comparison.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
        RefreshWindows();
    }
    private async Task CheckForUpdate()
    {
        if(checkingUpdate||readyInstaller!=null)return;
        if(controller.IsRunning){updateLabel.Text="فحص وتنزيل التحديث مؤجلان إلى ما بعد جلسة اللعب.";return;}
        if(string.IsNullOrWhiteSpace(ArabicRelease.Repository)){updateLabel.Text="قناة GitHub غير مرتبطة بعد.";return;}
        checkingUpdate=true;updateButton.IsEnabled=true;updateButton.Content="إلغاء الفحص / التنزيل";
        using var operation=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(updateCancel.Token);activeUpdateDownload=operation;
        try {
            updateLabel.Text="جارٍ فحص إصدارات GitHub…";
            var client=new GitHubUpdateClient(ArabicRelease.Repository);
            var release=await client.CheckAsync(Version.Parse(ArabicRelease.Version),operation.Token);
            if(release==null){updateLabel.Text="لا يوجد تحديث أحدث من نسختك — "+ArabicRelease.Version;return;}
            var folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArabicGamesLauncher","downloads",release.Version+"-"+Guid.NewGuid().ToString("N"));
            var zip=System.IO.Path.Combine(folder,"update.zip");
            updateLabel.Text="يتوفر "+release.Version+"؛ جارٍ التنزيل التلقائي…";
            await client.DownloadAsync(release,zip,new Progress<double>(value=>updateLabel.Text=$"تنزيل التحديث {release.Version}: {(int)(value*100)}%"),operation.Token);
            var extracted=System.IO.Path.Combine(folder,"package");
            await Task.Run(()=>GitHubUpdateClient.Extract(zip,extracted),operation.Token);
            readyInstaller=System.IO.File.Exists(System.IO.Path.Combine(extracted,"Reading-Fix.ps1"))?System.IO.Path.Combine(extracted,"Reading-Fix.ps1"):System.IO.Path.Combine(extracted,"Install.ps1");
            updateLabel.Text="التحديث "+release.Version+" جاهز ومتحقق منه. ثبته عندما تنتهي من اللعب.";
            updateButton.Content="تثبيت التحديث وإعادة التشغيل";
        }
        catch(OperationCanceledException){if(!updateCancel.IsCancellationRequested)updateLabel.Text="انتهت مهلة التحديث. ترجمة اللعبة مستمرة؛ أعد الفحص لاحقًا.";}
        catch { updateLabel.Text="تعذر فحص أو تنزيل تحديث موثوق. ترجمة اللعبة مستمرة؛ حاول لاحقًا."; }
        finally{activeUpdateDownload=null;checkingUpdate=false;updateButton.IsEnabled=true;if(readyInstaller==null)updateButton.Content="فحص التحديث";}
    }
    private void OpenXboxPlayer()
    {
        controller.Stop();controller.PreferPlayStation=false;controller.NativePlayStationEnabled=false;
        if(xboxPlayer is null){xboxPlayer=new XboxPlayerWindow();xboxPlayer.Closed+=(_,_)=>xboxPlayer=null;xboxPlayer.Show();}
        else {xboxPlayer.ResumeAfterRecording();xboxPlayer.WindowState=WindowState.Normal;xboxPlayer.Activate();}
        controller.Settings=controller.Settings with {DesktopWindowName="window:"+XboxPlayerWindow.PlayerTitle,VideoSource=VideoSourceType.DesktopScreen};
        resetWindowPreference=false;RefreshWindows();UpdateConsoleHelp();
    }

    private async Task PauseXboxForRecording(){if(xboxPlayer is null){status.Text="إن كان المصدر متصفحًا خارجيًا، أغلق جلسة Remote Play هناك ثم جرّب زر المشاركة. هذا الخيار يحرر البث المدمج فقط.";return;}controller.Stop();controller.StopSourcePreview();controller.ClearLastFrame();await xboxPlayer.PauseForRecordingAsync();status.Text="تحرير Remote Play للتسجيل (تجريبي): جرّب زر المشاركة في Xbox الآن. الترجمة متوقفة لغياب المصدر. للعودة اضغط فتح البث داخل التطبيق ثم ابدأ الترجمة. للتسجيل والترجمة معًا استخدم مصدر كرت التقاط/OBS دون Remote Play.";}

    private void InstallReadyUpdate()
    {
        if(readyInstaller==null)return;
        try {
            var process=new System.Diagnostics.ProcessStartInfo("powershell.exe"){UseShellExecute=false};
            foreach(var argument in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",readyInstaller})process.ArgumentList.Add(argument);
            if(System.IO.Path.GetFileName(readyInstaller)=="Reading-Fix.ps1"){process.ArgumentList.Add("-BasePath");process.ArgumentList.Add(AppContext.BaseDirectory);}
            System.Diagnostics.Process.Start(process);Close();
        }catch{updateLabel.Text="تعذر تشغيل المثبت؛ أعد المحاولة. النسخة الحالية لم تتغير.";}
    }
    private void ChooseEngine()
    { engineTestPassed=false;lastEngineCheck="لم يُختبر المحرك المختار بعد";key.Clear();controller.Stop();UpdateConnectionBar();controller.ArabicEngineChoice=engineChoice.SelectedIndex;LiveServices.GeminiModel=engineChoice.SelectedIndex==5?"gemini-3.5-flash-lite":null;controller.SessionTranslator=null;controller.Settings=controller.Settings with {Engine=engineChoice.SelectedIndex==1?TranslationEngine.DeepL:TranslationEngine.Gemini};RefreshCloudModels(); }
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
    private async void SaveKey(){
        if(testingKey)return;engineTestPassed=false;testingKey=true;lastEngineCheck="جارٍ اختبار المحرك…";UpdateConnectionBar();start.IsEnabled=false;testEngine.IsEnabled=false;azureRegion.IsEnabled=false;engineChoice.IsEnabled=false;cloudModel.IsEnabled=false;deepLMode.IsEnabled=false;key.IsEnabled=false;
        try{await SaveKeyCore();}finally{lastEngineCheck=status.Text;testingKey=false;UpdateConnectionBar();start.IsEnabled=true;testEngine.IsEnabled=true;azureRegion.IsEnabled=true;engineChoice.IsEnabled=true;cloudModel.IsEnabled=true;deepLMode.IsEnabled=true;key.IsEnabled=true;}
    }
    private void EngineSuccess(string? sample=null){
        if(sample!=null&&!ArabicOutputQuality.Accepts("Open the door.",sample)){engineTestPassed=false;status.Text="وصل رد من المحرك لكنه ليس ترجمة عربية صالحة. أعد الاختبار أو اختر محركًا آخر.";return;}
        engineTestPassed=true;status.Text="✓ نجح اختبار محرك الترجمة — أصبح جاهزًا لترجمة نص اللعبة. هذا لا يختبر ظهور النص على الشاشة."+(sample is null?"":"\nالعينة: «"+sample.Trim()+"»");
    }
    private async Task SaveKeyCore()
    {
        if(engineChoice.SelectedIndex==10){
            try {
                var value=string.IsNullOrWhiteSpace(key.Password)?SecureStore.Default.Load<string>(ProviderKeyId):key.Password.Trim();
                if(string.IsNullOrWhiteSpace(value)){status.Text="أدخل مفتاح مورد Azure Translator مرة واحدة.";return;}
                var translator=new AzureTranslator(value,azureRegion.Text);
                SecureStore.Default.Save(ProviderKeyId,value);SecureStore.Default.Save("arabic-azure-region",azureRegion.Text.Trim());key.Clear();
                status.Text="مفتاح Azure محفوظ؛ جارٍ اختبار الترجمة…";
                var watch=System.Diagnostics.Stopwatch.StartNew();var sample=await translator.TranslateAsync("Open the door.");watch.Stop();
                EngineSuccess(sample);
            }catch(TranslatorException error){status.Text="المفتاح محفوظ. Azure: "+error.Status+". عند الرفض تأكد من مفتاح مورد Translator ومنطقته؛ عند الحصة أو تعذر الخدمة أعد المحاولة لاحقًا.";}
             catch(ArgumentException){status.Text="تحقق من المفتاح ومنطقة Azure: حروف إنجليزية وأرقام وشرطة فقط، أو فارغة للمورد العالمي.";}
             catch{status.Text="تعذر حفظ أو اختبار Azure. لا تعاود إدخال المفتاح إن كان محفوظًا.";}return;
        }
        if(engineChoice.SelectedIndex>=8){
            try {
                var value=string.IsNullOrWhiteSpace(key.Password)?SecureStore.Default.Load<string>(ProviderKeyId):key.Password.Trim();
                if(string.IsNullOrWhiteSpace(value)){status.Text="أدخل مفتاح هذا المزوّد مرة واحدة.";return;}
                SecureStore.Default.Save(ProviderKeyId,value);key.Clear();status.Text="حُفظ المفتاح بتشفير Windows؛ جارٍ اختبار النموذج…";
                var result=await AiTranslator.TestAsync(CloudProvider,value,SelectedCloudModel);
                if(result.State==OnlineKeyState.Valid)EngineSuccess(result.Sample);else status.Text="لم ينجح اختبار المحرك: "+result.State+". المفتاح محفوظ؛ تحقق من الحصة والاتصال واسم النموذج.";
            }catch{status.Text="تعذر حفظ المفتاح أو اختبار النموذج؛ تحقق من الاتصال والمزوّد.";}return;
        }
        if(engineChoice.SelectedIndex==7){status.Text="جارٍ اختبار Google بدون مفتاح…";try{EngineSuccess(await new GoogleGtxTranslator().TranslateAsync("Open the door."));}catch{status.Text="تعذر اختبار Google. تحقق من الإنترنت أو جرّب لاحقًا.";}return;}
        if(IsLocalModel){try{var sample=await LocalTranslator().TranslateAsync("Open the door.");EngineSuccess(sample);}catch(Exception error){status.Text=LocalError(error);}return;}
        if (string.IsNullOrWhiteSpace(key.Password) && !(engineChoice.SelectedIndex==1?controller.HasDeepLKey:controller.HasGeminiKey)) { status.Text = "أدخل المفتاح مرة واحدة؛ لا يوجد مفتاح محفوظ لهذا المحرك."; return; }
        status.Text = "جارٍ اختبار المفتاح؛ قد يستغرق الطلب الأول حتى 25 ثانية…";
        try
        {
            var result = engineChoice.SelectedIndex==1 ? (string.IsNullOrWhiteSpace(key.Password)?await controller.CheckDeepLKeyAsync():await controller.SetDeepLKeyAsync(key.Password)) : (string.IsNullOrWhiteSpace(key.Password)?await controller.TestSavedGeminiKeyAsync():await controller.SetGeminiKeyAsync(key.Password,persistUnverified:true));
            bool saved=result.State==OnlineKeyState.Valid;
            if(saved)key.Clear();
            if(saved&&engineChoice.SelectedIndex==1){
                try{var watch=System.Diagnostics.Stopwatch.StartNew();var sample=await controller.TestSavedDeepLTranslationAsync();watch.Stop();EngineSuccess(sample);}
                catch(TranslatorException error){status.Text="المفتاح محفوظ لكن اختبار وضع DeepL فشل: "+error.Status+". إذا كان الوضع غير مدعوم اختر تلقائيًا وأعد الاختبار.";}
                catch{status.Text="مفتاح DeepL محفوظ؛ تعذر اختبار الترجمة الآن. جرّب الوضع التلقائي أو أعد المحاولة لاحقًا.";}
                return;
            }
            if(saved){EngineSuccess();return;}
            status.Text= (controller.HasGeminiKey && engineChoice.SelectedIndex!=1?"المفتاح محفوظ. نتيجة الاتصال: ":"نتيجة الاختبار: ")+result.State+(engineChoice.SelectedIndex!=1?"\n"+LiveServices.LastKeyCheckDetail:"");
        }
        catch { status.Text = "تعذر الاتصال بالمحرك المختار. المفتاح ما زال في الخانة."; }
    }
    private void UpdateConsoleHelp()
    {
        if(capturePicker!=null)capturePicker.Visibility=DirectPs5?Visibility.Collapsed:Visibility.Visible;
        sourceInstruction.Text=DirectPs5?"PS5 مباشر: أكمل الاقتران ثم اختر المحرك وابدأ الترجمة؛ لا تختار نافذة فيديو.":"فعّل النص الإنجليزي في اللعبة، وافتح البث ثم اختر نافذته.";
        consoleHelp.Text=DirectPs5?"PS5 مباشر: تسجيل Sony بالثانوي، اكتشاف الجهاز ورمز الاقتران داخل المترجم. الأساسي للعبة واليد؛ لا تحتاج فتح Sony Remote Play أو نافذة فيديو خارجية.":controller.PreferPlayStation
            ? "مصدر PS5: فعّل Settings > System > Remote Play > Enable Remote Play في الجهاز. افتح PS Remote Play على Windows وسجّل الدخول بحسابك الرسمي، ثم اختر PS5 وانتظر الفيديو. اضغط تحديث النوافذ إذا لم تُحدّد نافذته. ربط التلفزيون والمحركات كما هما؛ هذا ليس اتصالًا أصليًا داخل المترجم."
            : "مصدر Xbox: افتح جلسة Remote Play الرسمية واختر نافذة الفيديو. العب على HDMI مباشرة واجعل البث للقراءة فقط إذا رغبت.";
    }
    private void ConfigurePlayStation(){
        controller.Stop();controller.PreferPlayStation=true;controller.NativePlayStationEnabled=true;UpdateConsoleHelp();
        new NativePlayStationWindow(controller){Owner=this}.ShowDialog();UpdateConsoleHelp();
    }
    private void LaunchPlayStation()
    {
        controller.Stop();controller.PreferPlayStation=true;controller.NativePlayStationEnabled=false;resetWindowPreference=true;UpdateConsoleHelp();
        var paths=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}
            .Select(root=>System.IO.Path.Combine(root,"Sony","PS Remote Play","RemotePlay.exe"));
        var installed=paths.FirstOrDefault(System.IO.File.Exists);
        try {
            if(installed!=null)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installed){UseShellExecute=true});
            else OpenUrl("https://www.playstation.com/en-us/remote-play/");
        }catch{status.Text="افتح PS Remote Play يدويًا ثم اضغط تحديث النوافذ.";}
        RefreshWindows();
        var retry=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        retry.Tick+=(_,_)=>{retry.Stop();if(!controller.IsRunning)RefreshWindows();};retry.Start();
    }
    private void RefreshWindows()
    {
        var old=resetWindowPreference?null:(windows.SelectedItem as ComboBoxItem)?.Tag as string??controller.Settings.DesktopWindowName;
        resetWindowPreference=false;windows.Items.Clear();
        var titles=WindowCaptureService.GetTopLevelWindowTitles();string? detected=null;
        if(controller.PreferPlayStation)foreach(var processName in new[]{"RemotePlay","chiaki","chiaki-ng"}){
            foreach(var process in System.Diagnostics.Process.GetProcessesByName(processName)){
                using(process){try{if(!string.IsNullOrWhiteSpace(process.MainWindowTitle)){detected=process.MainWindowTitle;break;}}catch{}}
            }
            if(detected!=null)break;
        }
        var preferred=ConsoleWindowSelection.Pick(titles,controller.PreferPlayStation,detected);
        foreach(var title in titles){
            var item=new ComboBoxItem{Content=title,Tag="window:"+title};windows.Items.Add(item);
            if((string)item.Tag==old||old==null&&title==preferred)windows.SelectedItem=item;
        }
        foreach(var monitor in WindowCaptureService.GetDisplayMonitors()){
            var item=new ComboBoxItem{Content="الشاشة كاملة: "+monitor.Name,Tag="monitor:"+monitor.Index};windows.Items.Add(item);if((string)item.Tag==old)windows.SelectedItem=item;
        }
    }
    private static string EngineStatus(TranslatorStatus value)=>value switch{TranslatorStatus.OutputRejected=>"الناتج يحتاج إعادة ترجمة",TranslatorStatus.ServiceBusy=>"المحرك مشغول",TranslatorStatus.EngineFailed=>"خطأ في تشغيل المحرك",TranslatorStatus.ServiceUnreachable=>"الخدمة غير متاحة / انتهت المهلة",TranslatorStatus.ModelRejected=>"النموذج الجاري لا يطابق الاختيار",TranslatorStatus.KeyRejected=>"المفتاح أو رمز الربط مرفوض",_=>value.ToString()};
    private void UpdateConnectionBar()
    {
        var engineState = testingKey ? "جارٍ الاختبار" : !controller.IsRunning ? "متوقف" : controller.TranslatorStatus==TranslatorStatus.Ready ? "يعمل" : EngineStatus(controller.TranslatorStatus);
        var check=lastEngineCheck.Replace("\n"," · ");if(check.Length>150)check=check[..150]+"…";
        bool connected=controller.TvClients>0;connectionBar.Background=new SolidColorBrush(testingKey?Color.FromRgb(255,226,154):engineTestPassed&&connected&&(!controller.IsRunning||controller.TranslatorStatus==TranslatorStatus.Ready)?Color.FromRgb(141,231,182):connected?Color.FromRgb(173,216,255):Color.FromRgb(227,232,242));connectionBar.Foreground=new SolidColorBrush(Color.FromRgb(18,34,48));connectionBar.FontWeight=FontWeights.Bold;connectionBar.Padding=new Thickness(10,6,10,6);
        connectionBar.Text=$"المحرك: {engineState} | الشاشة: {(controller.TvClients>0 ? "متصلة ("+controller.TvClients+")" : "غير متصلة")} | الترجمة: {(controller.IsRunning ? "تعمل" : "متوقفة")}\nآخر اختبار: {check}";connectionBar.ToolTip=lastEngineCheck;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(AppController.LastFrame))return;
        if(e.PropertyName==nameof(AppController.IsRunning)) {
            if(controller.IsRunning)activeUpdateDownload?.Cancel();
            else if(readyInstaller==null&&controller.AutoUpdateEnabled)_=CheckForUpdate();
        }
        connectionInfo.Text=$"عنوان الكمبيوتر: {controller.ComputerAddress ?? "غير متصل"} | المنفذ: {controller.ServerPort} | شاشات: {controller.TvClients}";
        UpdateConnectionBar();
        start.Content = controller.IsRunning ? "■ إيقاف الترجمة" : "▶ بدء الترجمة";
        source.Text = controller.LastSource;
        translated.Text = controller.LastTranslation;
        if (controller.LastLineMetrics is { } m)
            timing.Text = m.Origin==LineOrigin.Pack ? $"ترجمة من قاموس المراجعة — بدون طلب للمحرك | الإجمالي: {m.TotalMs} ms" : $"القراءة: {m.OcrMs} ms | الترجمة: {m.TranslationMs} ms | الإجمالي داخل البرنامج: {m.TotalMs} ms";
        status.Text = controller.OcrMissing ? "حزمة قراءة الإنجليزية مفقودة: ثبّت English من إعدادات لغات Windows."
            : controller.IsRunning ? "الترجمة تعمل — "+(controller.TranslatorStatus switch {
                TranslatorStatus.ServiceUnreachable=>"تعذر الاتصال بخدمة الترجمة أو انتهت المهلة؛ المفتاح لم يُحذف.",
                TranslatorStatus.KeyRejected=>"خدمة الترجمة رفضت المفتاح.",
                TranslatorStatus.QuotaExceeded=>"وصلت إلى حد الطلبات أو انتهت حصة الخدمة. لا تحتاج تنزيل نموذج.",
                TranslatorStatus.ModelRejected=>"الخدمة رفضت اسم النموذج المختار. جرّب Gemini Flash-Lite.",
                TranslatorStatus.EngineFailed=>"تعذر تشغيل محرك الترجمة.",
                TranslatorStatus.OutputRejected=>"المحرك أعاد إنجليزية أو صياغة عامية؛ تُعاد المحاولة دون حذف النموذج. راجع سجل النصوص.",
                TranslatorStatus.ServiceBusy=>"المحرك مشغول؛ الحوار له أولوية وتُؤجّل رسائل الجوال.",
                _=>"حالة الترجمة: "+controller.TranslatorStatus }) : "متوقف";
    }
    private static void OpenUrl(string url)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
    private void ShowActivationSteps()
    {
        var tabs=new TabControl{Margin=new Thickness(12)};
        void Page(string title,string text){tabs.Items.Add(new TabItem{Header=title,Content=new ScrollViewer{Content=Label(text,17),VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});}
        Page("Xbox","1. فعّل الميزات البعيدة في Xbox.\n2. من مصدر اللعبة اضغط فتح البث داخل التطبيق، وسجّل الدخول في صفحة Microsoft الرسمية ثم افتح كونسولك.\n3. ارجع للمترجم واضغط معاينة المصدر؛ افتح مناطق القراءة لضبط المربعات. نافذة البث تبقى شغالة عند التنقل.\n4. يمكنك بدلًا من ذلك اختيار نافذة Xbox أو متصفح خارجي من القائمة.\n5. اختر المحرك واختبره، واربط الشاشة ثم ابدأ الترجمة. للعب الأقل تأخرًا استخدم صورة HDMI الأصلية.");
        Page("PS5","1. أضف حساب PSN ثانويًا مجانيًا على PS5 وفعّل اللعب عن بُعد وأنت به.\n2. اضغط PS5 — اقتران مباشر في مترجمنا وسجّل بالحساب الثانوي نفسه في صفحة Sony.\n3. اكتشف PS5 تلقائيًا، وافتح ربط جهاز على الكونسول واكتب رمز الـ8 أرقام هنا.\n4. ارجع للأساسي من يد PS5 وافتح لعبتك، واترك الثانوي للبث.\n5. اختر المحرك وابدأ الترجمة؛ لا تحتاج نافذة Sony أو مشاركة الشاشة. أغلق جلسات Remote Play الخارجية.\nيحفظ الحساب والربط مشفّرًا؛ لا نحتفظ بكلمة مرور Sony.");
        Page("الشاشة","Google TV / Android TV: ثبّت تطبيق الشاشة، واضغط اكتشاف الجوال أو الكمبيوتر ثم اختر جهازك. زر إصلاح الاتصال يعيد الربط دون مسح الإعدادات.\nالمدخل: اختر HDMI واضغط عرض اللعبة؛ العودة للعب تخفي الواجهة، وOK أو رجوع يُظهرها.\nإذا نقلتك الشاشة إلى تطبيق HDMI آخر، أوقف HDMI-CEC من إعدادات التلفزيون؛ HDR لا نغيره. إصلاح الصورة منفصل عن إصلاح الاتصال.\nLG/Samsung يحتاجان تثبيت حزمهما الخاصة. المونيتور العادي لا يعرض طبقة تلقائيًا فوق HDMI؛ استخدم عرض الكمبيوتر أو صفحة شاشة ثانية بجهاز آخر.\nاسمح للمترجم في جدار حماية Windows على الشبكة الخاصة. الإدخال اليدوي يقبل IP أو IP:منفذ، كما هو ظاهر في الكمبيوتر.");
        Page("المحرك والمناطق","1. اختر QuickMT ثم تنزيل/تشغيل وانتظر Ready، أو اختر مزودًا سحابيًا وافتح رابط API بجانبه.\n2. اضغط حفظ واختبار المحرك؛ المفتاح الفارغ يبقي المحفوظ. نموذج التنزيل المحلي يبقى محفوظًا، ولا تحذفه عند تحديث التطبيق.\n3. في مناطق القراءة: الحوار مستقل. الأصفر للقراءة والأزرق لظهور العربية؛ لكل منهما يسار/أعلى/عرض/ارتفاع بالأرقام. أضف جوالًا أو قائمة أو بطاقة مهام، وحدد الخط والخلفية والإطار والمدة. احفظ التخطيط وشاركه من التصدير.\n4. يمكن ضبط المناطق أثناء المعاينة أو اللعب. أوقف المنطقة غير اللازمة لتخفيف الحمل.\n5. 429 حصة/معدل طلبات، و503 خدمة غير متاحة مؤقتًا؛ تغيير المفتاح وحده لا يعالج حد المشروع.\n6. السجل محلي، ويمكن تصديره من صفحة السجل والتحديث.");
        new Window{Title="التفعيل خطوة بخطوة",Owner=this,Width=760,Height=630,Content=tabs,Background=Background,Foreground=Foreground,FlowDirection=FlowDirection.RightToLeft}.ShowDialog();
    }
    private void RefreshCloudModels()
    {
        credentialPanel.Visibility=IsLocalModel||engineChoice.SelectedIndex==7?Visibility.Collapsed:Visibility.Visible;
        localLauncher.Visibility=IsLocalModel?Visibility.Visible:Visibility.Collapsed;
        foreach(var element in credentialPanel.Children.OfType<FrameworkElement>())if(Equals(element.Tag,"azure-region-group"))element.Visibility=engineChoice.SelectedIndex==10?Visibility.Visible:Visibility.Collapsed;
        testEngine.Content=IsLocalModel?"اختبار النموذج المحلي":engineChoice.SelectedIndex==7?"اختبار Google بدون مفتاح":"حفظ واختبار المحرك";
        engineHint.Text=engineChoice.SelectedIndex switch{
            2=>"QuickMT: نقطة المقارنة المحلية الأسرع في سجلك السابق. اختبر نفس الحوارات؛ ليس مضمونًا أن يترجم كل تعبير بدقة.",
            3 or 4=>"Jisr: محلي وخفيف؛ قارن اللهجة والمعنى على المشهد نفسه.",
            6=>"HY-MT: محلي وتجريبي، أثقل على المعالج. لا يُنصح به كخيار سرعة لهذا اللابتوب دون قياس.",
            1=>"DeepL: جرّب تلقائيًا أولًا؛ يمكنك مقارنة وضع السرعة ووضع الجودة. اسم اللعبة والحوار السابق يساعدان في السياق.",
            7=>"Google بدون مفتاح: خدمة إنترنت تجريبية، وليست Google Cloud API الرسمي. يرسل نص الحوار فقط وقد يحدد الطلبات.",
            10=>"Azure: خيار رسمي جديد للمقارنة؛ يحتاج مفتاح مورد Translator ومنطقته إن كان إقليميًا. لا يوجد إثبات أنه أدق من DeepL على لعبتك بعد.",
            _=>"AI: يرسل نص الحوار وسياقه للمزوّد. اختر النموذج ثم اختبر؛ حصة API مستقلة عن اشتراك تطبيق المحادثة. لا نبدّل المحرك تلقائيًا عند الخطأ."};
        engineHelp.Content=IsLocalModel?"تنزيل / تشغيل النموذج":engineChoice.SelectedIndex==7?"بدون مفتاح — التفاصيل ↗":"كيف تحصل على API؟ ↗";
        deepLMode.Visibility=engineChoice.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
        cloudModel.Items.Clear();
        cloudModel.Visibility=engineChoice.SelectedIndex is 0 or 5 or 8 or 9?Visibility.Visible:Visibility.Collapsed;
        if(cloudModel.Visibility!=Visibility.Visible)return;
        var provider=CloudProvider;
        foreach(var model in provider.Models())cloudModel.Items.Add(model.Id);
        var saved=SecureStore.Default.Load<string>("arabic-cloud-model:"+provider.Id());
        var preferred=engineChoice.SelectedIndex==5?"gemini-3.5-flash-lite":saved??provider.DefaultModel().Id;
        cloudModel.SelectedItem=preferred;
        if(cloudModel.SelectedIndex<0)cloudModel.SelectedIndex=0;
    }
    private void OpenEnginePage()
    {
        var url=engineChoice.SelectedIndex switch {
            1=>"https://www.deepl.com/your-account/keys",
            10=>"https://learn.microsoft.com/azure/ai-services/translator/create-translator-resource",
            8=>"https://platform.openai.com/api-keys",
            9=>"https://platform.claude.com/settings/keys",
            2=>"https://huggingface.co/quickmt",
            3 or 4=>"https://huggingface.co/models?search=Jisr",
            6=>"https://huggingface.co/tencent/HY-MT1.5-1.8B",
            7=>"https://cloud.google.com/translate",
            _=>"https://aistudio.google.com/apikey"};
        if(IsLocalModel){StartLocalModel();return;}
        OpenUrl(url);
    }
    private async void ShowTvConnections()
    {
        var panel=new StackPanel{Margin=new Thickness(20)};
        var info=Label("جارٍ البحث عن LG وSamsung على الشبكة المحلية…");panel.Children.Add(info);
        var found=new StackPanel();panel.Children.Add(found);
        panel.Children.Add(Label("LG webOS: ثبّت حزمة الشاشة مرة واحدة عبر Developer Mode، ثم وافق على طلب الاقتران الذي يظهر على التلفزيون. اكتشاف الجهاز وحده لا يثبت التطبيق."));
        panel.Children.Add(Button("تحميل حزمة LG webOS وتعليمات التثبيت",()=>OpenUrl("https://github.com/Tariq-220/ArabicGamesTranslator/releases/latest")));
        panel.Children.Add(Button("تحميل تطبيق Google TV / Android TV — APK الشاشة",()=>OpenUrl($"https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/preview-{ArabicRelease.Version}/ArabicGames-TV-{ArabicRelease.TvVersion}.apk")));
        panel.Children.Add(Button("Samsung Tizen — نسخة مطوّر تحتاج توقيعًا وخطوات التثبيت",()=>OpenUrl("https://github.com/Tariq-220/ArabicGamesTranslator/releases")));
        panel.Children.Add(Label("TCL / Google TV: حدّث تطبيق الشاشة ثم اختر اكتشاف الكمبيوتر. إذا وُجد أكثر من كمبيوتر تختار واحدًا؛ لن نربط جهازًا عشوائيًا."));
        panel.Children.Add(Button("Monitor / شاشة غير مدعومة: عرض باركود الشاشة الثانية",ShowSecondScreen));
        var window=new Window{Title="ربط الشاشات",Owner=this,Width=650,Height=590,Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Background=Background,Foreground=Foreground,FlowDirection=FlowDirection.RightToLeft};
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative)});
        window.Show();
        try {
            var tvs=await controller.DiscoverTvsAsync();
            if(!window.IsVisible)return;
            info.Text=tvs.Count==0?"لم نجد شاشة LG أو Samsung. تأكد من الشبكة وشغّل الشاشة؛ قد يمنع الراوتر الاكتشاف.":"اختر الشاشة ثم وافق على طلب التلفزيون إن ظهر.";
            foreach(var tv in tvs)found.Children.Add(Button(tv.Name+" — "+tv.Brand,async()=>{
                info.Text="جارٍ الاقتران؛ راقب طلب السماح على التلفزيون…";
                try{await controller.PairAsync(tv);info.Text="تم حفظ الشاشة. افتح تطبيق الترجمة عليها واضغط اختبار العربية. استقبال السطور: "+controller.TvClients;}
                catch{info.Text="تعذر الاقتران. تحقق من إذن الشاشة وتثبيت تطبيقها والشبكة.";}
            }));
        }catch{info.Text="تعذر البحث عن الشاشات؛ تحقق من الشبكة الخاصة وإذن جدار الحماية.";}
    }
    private void ShowSecondScreen()
    {
        var url=$"http://{controller.ComputerAddress??"127.0.0.1"}:{controller.ServerPort}/screen";
        using var generator=new QRCoder.QRCodeGenerator();using var data=generator.CreateQrCode(url,QRCoder.QRCodeGenerator.ECCLevel.M);using var code=new QRCoder.PngByteQRCode(data);
        using var stream=new System.IO.MemoryStream(code.GetGraphic(8));var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
        var content=new StackPanel{Margin=new Thickness(20)};content.Children.Add(Label("امسح من جهاز على الشبكة نفسها. اللابتوب يبقى يعمل؛ هذه صفحة تعرض النص فقط."));content.Children.Add(new Image{Source=bitmap,Width=300,Height=300});content.Children.Add(new InputBox{Text=url,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight});content.Children.Add(Label("رابط الكمبيوتر متاح على الشبكة المحلية دون رمز؛ استخدم شبكة خاصة موثوقة ولا تفتح المنفذ على الإنترنت."));
        var window=new Window{Title="شاشة ثانية",Owner=this,Width=470,Height=550,Content=content,Background=Background,Foreground=Foreground,FlowDirection=FlowDirection.RightToLeft};window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative)});window.ShowDialog();
    }
    private static TextBlock Label(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(4, 6, 4, 6), TextWrapping = TextWrapping.Wrap };
    private static WrapPanel Row() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
    private static Button Button(string text, Action action)
    {
        var b = new Button { MinHeight=48, FontSize=16, Content = text, Foreground = Brushes.White, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4) };
        b.Click += (_, _) => action(); return b;
    }
    private static Border Card(UIElement child) => new() { Child = child, Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 6), Background = new SolidColorBrush(Color.FromRgb(30, 40, 56)), CornerRadius = new CornerRadius(8) };
}
