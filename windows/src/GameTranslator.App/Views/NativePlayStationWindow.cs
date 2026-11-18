// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>Arabic direct PS5 setup over the existing chiaki registration and session services.</summary>
public sealed class NativePlayStationWindow : Window
{
 private readonly AppController controller;
 private readonly StackPanel body=new(){Margin=new Thickness(18)};
 private TextBlock status=new();private bool busy,closed;private int step;
 public NativePlayStationWindow(AppController controller)
 {
  this.controller=controller;Title="PS5 — ربط مباشر داخل المترجم";Width=680;Height=760;MinWidth=520;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;FlowDirection=FlowDirection.RightToLeft;Background=new SolidColorBrush(Color.FromRgb(15,23,36));Foreground=Brushes.White;
  Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative)});
  Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,PanningMode=PanningMode.VerticalOnly};step=controller.Console!=null&&controller.HasAccount?3:controller.HasAccount?2:0;Closed+=(_,_)=>closed=true;Render();
 }
 private TextBlock Label(string value,int size=16)=>new(){Text=value,FontSize=size,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,8,4,8)};
 private Button Action(string value,Action click){var b=new Button{Content=value,MinHeight=48,Margin=new Thickness(4,6,4,6),Padding=new Thickness(12,8,12,8)};b.Click+=(_,_)=>{if(!busy)click();};body.Children.Add(b);return b;}
 private void Render()
 {
  body.Children.Clear();body.Children.Add(Label("PS5 · الربط المباشر — "+(step+1)+" / 4",24));
  if(!StreamSession.IsNativeAvailable)body.Children.Add(Label("المكتبة الأصلية لـPS5 غير متاحة. يلزم تحديث الكمبيوتر الذي يتضمن محرك PS5؛ لن نفتح برنامج Sony بدلًا منه.",14));
  if(step==0){body.Children.Add(Label("1. أضف حساب PSN ثانويًا مجانيًا على PS5 وسجّل دخوله.\n2. وأنت بالثانوي: الإعدادات ← النظام ← اللعب عن بُعد ← تمكين اللعب عن بُعد.\n3. نستخدم الثانوي للبث فقط؛ الأساسي للعبة ويد PS5. لا تحتاج شراء اللعبة بالثانوي أو تشغيل Sony Remote Play الخارجي."));Action("جهزت الثانوي على PS5 — التالي",()=>{step=1;Render();});}
  else if(step==1){body.Children.Add(Label("سجّل في صفحة Sony بالحساب الثانوي نفسه الموجود على PS5. كلمة المرور لدى Sony فقط؛ يحفظ المترجم معرّف الحساب وبيانات الاقتران بتشفير Windows."));Action("تسجيل Sony بالثانوي",Login);}
  else if(step==2){body.Children.Add(Label("الجهازان على نفس الشبكة. اكتشف PS5 واختره ثم أدخل رمز ربط الجهاز الذي يظهر على PS5 وأنت بالثانوي."));Action("اكتشاف PS5 تلقائيًا",Discover);}
  else{body.Children.Add(Label("PS5 المحفوظ: "+controller.Console?.Name+"\nارجع لحسابك الأساسي من يد PS5 وافتح اللعبة، واترك الثانوي للبث. فعّل Subtitles الإنجليزية وأغلق جلسات Remote Play الخارجية.\nمن نافذة المترجم اختر محركك ثم بدء الترجمة؛ سيأتي الفيديو مباشرة من PS5 دون التقاط نافذة خارجية."));Action("استخدام PS5 المباشر والرجوع للمترجم",()=>{controller.NativePlayStationEnabled=true;controller.PreferPlayStation=true;DialogResult=true;});Action("إعادة الاكتشاف والاقتران",()=>{step=2;Render();});}
  status=Label("",14);body.Children.Add(status);if(step>0)Action("السابق",()=>{step--;Render();});Action("إغلاق",Close);
 }
 private async void Login()
 {
  if(!StreamSession.IsNativeAvailable){status.Text="ثبّت تحديث محرك PS5 أولًا.";return;}busy=true;
  try{var redirect=PsnLoginWindow.Show(this);if(redirect==null){status.Text="لم يكتمل دخول Sony.";return;}status.Text="جارٍ حفظ حساب Sony…";await controller.CompletePsnLoginAsync(redirect);if(closed)return;step=2;Render();}
  catch(PsnAccountException e){status.Text="تعذر تسجيل Sony"+(e.StatusCode is {} code?" (HTTP "+code+")":"")+". أعد المحاولة بالحساب الثانوي.";}
  catch{status.Text="تعذر الاتصال أو حفظ بيانات Sony الآمنة. لم نحذف مفاتيح الترجمة.";}
  finally{busy=false;}
 }
 private async void Discover()
 {
  if(!StreamSession.IsNativeAvailable||!controller.HasAccount){status.Text="أكمل دخول Sony وثبّت محرك PS5 أولًا.";return;}busy=true;status.Text="جارٍ اكتشاف PS5…";
  try{var found=await controller.DiscoverConsolesAsync();if(closed)return;status.Text=found.Count==0?"لم نجد PS5. شغّله وتأكد من الشبكة والميزات البعيدة.":"اختر PS5. العنوان يملأ تلقائيًا.";foreach(var item in found){var console=item;Action(console.Name+(console.IsAwake?" · يعمل":" · وضع الراحة"),()=>Pin(console));}}
  catch{status.Text="تعذر اكتشاف PS5. تأكد من الشبكة الخاصة وإذن جدار حماية Windows.";}
  finally{busy=false;}
 }
 private static bool PrivateHost(string host){if(!System.Net.IPAddress.TryParse(host,out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)return false;var b=ip.GetAddressBytes();return b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31;}
 private void Pin(ConsoleInfo console)
 {
  if(!PrivateHost(console.Host)){status.Text="يجب أن يكون عنوان PS5 على الشبكة المحلية.";return;}
  body.Children.Clear();body.Children.Add(Label("رمز الاقتران · "+console.Name,23));body.Children.Add(Label("على PS5 وأنت بالحساب الثانوي: الإعدادات ← النظام ← اللعب عن بُعد ← ربط جهاز. اكتب الـ8 أرقام واترك شاشة الرمز مفتوحة."));
  var pin=new PasswordBox{MaxLength=8,MinHeight=48,FlowDirection=FlowDirection.LeftToRight,Margin=new Thickness(4)};body.Children.Add(pin);status=Label("",14);body.Children.Add(status);
  Action("اقتران وحفظ PS5",async()=>{if(pin.Password.Length!=8||pin.Password.Any(c=>c<'0'||c>'9')){status.Text="رمز PS5 يجب أن يكون 8 أرقام.";return;}busy=true;try{var value=pin.Password;pin.Clear();status.Text="جارٍ الاقتران؛ أبقِ PS5 على شاشة الرمز…";await controller.RegisterAsync(console.Host,value);if(closed)return;step=3;Render();}catch(AppException e){status.Text=e.Error switch{AppError.WrongAccount=>"الحساب لا يطابق مستخدم PS5. سجّل نفس الثانوي.",AppError.InvalidPin=>"الرمز غير صحيح أو انتهى؛ افتح رمزًا جديدًا.",_=>"لم يكتمل الاقتران. تأكد من الثانوي وشاشة ربط جهاز والشبكة."};}catch{status.Text="تعذر الاتصال بمحرك PS5 أو الجهاز. الربط السابق ومفاتيح الترجمة محفوظة.";}finally{busy=false;}});
  Action("الرجوع للاكتشاف",()=>{step=2;Render();});
 }
}
