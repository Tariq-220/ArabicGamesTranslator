// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using GameTranslator.Core;
using GameTranslator.Shell;
using Frame=GameTranslator.Core.Frame;
using TextBox=System.Windows.Controls.TextBox;
namespace GameTranslator.App.Controls;

/// <summary>Live normalized region editor. Source/output rectangles use the same coordinate space
/// as captured frames, independent of window size or display scaling. Mouse and touch share gestures.</summary>
public sealed class ReadingAreaEditor : UserControl
{
    private readonly AppController controller;
    private readonly List<ReadingArea> areas;
    private readonly ListBox list=new(){MinWidth=180,MaxHeight=210};
    private readonly Canvas canvas=new(){Width=1280,Height=720,Background=Brushes.Black,ClipToBounds=true};
    private readonly Image image=new(){Width=1280,Height=720,IsHitTestVisible=false};
    private readonly TextBox name=new(){MinWidth=180,MinHeight=40};
    private readonly CheckBox enabled=new(){Content="تفعيل المنطقة",MinHeight=40};
    private readonly ComboBox mode=new(){MinHeight=40};
    private readonly Slider font=new(){Minimum=14,Maximum=48,TickFrequency=1,IsSnapToTickEnabled=true};
    private readonly Slider background=new(){Minimum=0,Maximum=100};
    private readonly TextBlock note=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(350)};
    private bool updating,dragging,resize,draw;
    private Point anchor;
    private CropRegion original=new();
    private Frame? last;
    private ReadingArea? Selected=>list.SelectedIndex>=0&&list.SelectedIndex<areas.Count?areas[list.SelectedIndex]:null;
    private bool Output=>mode.SelectedIndex==1;

    public ReadingAreaEditor(AppController controller)
    {
        this.controller=controller;
        areas=[new("dialogue","الحوار",ReadingAreaKind.Dialogue,controller.Settings.Region,new(.78,.96,.14,.86),FontSize:controller.Settings.Style.FontSize,Background:controller.Settings.Style.Background),..controller.ReadingAreas];
        var root=new StackPanel{Margin=new Thickness(10)};
        root.Children.Add(new TextBlock{Text="مناطق القراءة · اسحب داخل المربع لتحريكه، أو الزاوية لتغيير حجمه. يمكنك تعديلها أثناء البث.",TextWrapping=TextWrapping.Wrap,FontSize=17});
        var preview=new Viewbox{Stretch=Stretch.Uniform,Child=canvas,MaxHeight=440,Margin=new Thickness(0,12,0,8)};root.Children.Add(preview);
        mode.Items.Add("مربع القراءة من اللعبة");mode.Items.Add("مربع ظهور العربية");mode.SelectedIndex=0;root.Children.Add(mode);
        root.Children.Add(list);
        var actions=new WrapPanel();
        actions.Children.Add(Action("+ رسائل جوال",()=>Add(ReadingArea.Phone())));
        actions.Children.Add(Action("+ قائمة / نص",()=>Add(ReadingArea.Menu())));
        actions.Children.Add(Action("رسم مربع جديد",()=>{draw=true;note.Text="اسحب على المعاينة لرسم المربع المحدد.";}));
        actions.Children.Add(Action("حذف المنطقة",Remove));root.Children.Add(actions);
        root.Children.Add(name);root.Children.Add(enabled);
        root.Children.Add(new TextBlock{Text="حجم خط المنطقة"});root.Children.Add(font);
        root.Children.Add(new TextBlock{Text="خلفية المنطقة · صفر = بدون خلفية"});root.Children.Add(background);
        root.Children.Add(note);Content=root;
        list.SelectionChanged+=(_,_)=>LoadSelection();mode.SelectionChanged+=(_,_)=>Render();
        name.TextChanged+=(_,_)=>Edit(a=>a with{Name=name.Text});
        enabled.Checked+=(_,_)=>Edit(a=>a with{Enabled=true});enabled.Unchecked+=(_,_)=>Edit(a=>a with{Enabled=false});
        font.ValueChanged+=(_,_)=>Edit(a=>a with{FontSize=(int)font.Value});
        background.ValueChanged+=(_,_)=>Edit(a=>a with{Background=(int)background.Value});
        canvas.MouseLeftButtonDown+=(_,e)=>{Begin(e.GetPosition(canvas));canvas.CaptureMouse();e.Handled=true;};
        canvas.MouseMove+=(_,e)=>{if(dragging)Move(e.GetPosition(canvas));};
        canvas.MouseLeftButtonUp+=(_,e)=>{End();canvas.ReleaseMouseCapture();e.Handled=true;};
        canvas.TouchDown+=(_,e)=>{Begin(e.GetTouchPoint(canvas).Position);e.TouchDevice.Capture(canvas);e.Handled=true;};
        canvas.TouchMove+=(_,e)=>{if(dragging)Move(e.GetTouchPoint(canvas).Position);e.Handled=true;};
        canvas.TouchUp+=(_,e)=>{End();e.TouchDevice.Capture(null);e.Handled=true;};
        canvas.LostMouseCapture+=(_,_)=>End();
        saveTimer.Tick+=(_,_)=>{saveTimer.Stop();Save();};
        name.LostKeyboardFocus+=(_,_)=>{var index=list.SelectedIndex;if(index>=0)RebuildList(index);};
        timer.Tick+=(_,_)=>RefreshFrame();Loaded+=(_,_)=>timer.Start();Unloaded+=(_,_)=>{timer.Stop();if(saveTimer.IsEnabled){saveTimer.Stop();Save();}};
        RebuildList(0);
    }
    private static Button Action(string title,Action run){var b=new Button{Content=title,MinHeight=44,Margin=new Thickness(4),Padding=new Thickness(10)};b.Click+=(_,_)=>run();return b;}
    private void Add(ReadingArea area){if(areas.Count>=9){note.Text="الحد الأقصى 8 مناطق إضافية لحماية أداء الجهاز.";return;}areas.Add(area);RebuildList(areas.Count-1);Save();}
    private void Remove(){if(list.SelectedIndex<=0)return;var i=list.SelectedIndex;areas.RemoveAt(i);RebuildList(Math.Min(i,areas.Count-1));Save();}
    private void RebuildList(int index){updating=true;list.Items.Clear();foreach(var area in areas)list.Items.Add(area.Name);updating=false;list.SelectedIndex=index;LoadSelection();}
    private void LoadSelection(){if(updating||Selected is not {} area)return;updating=true;name.Text=area.Name;enabled.IsChecked=area.Enabled;font.Value=area.FontSize;background.Value=area.Background;enabled.IsEnabled=area.Id!="dialogue";name.IsEnabled=area.Id!="dialogue";mode.IsEnabled=area.Id!="dialogue";if(area.Id=="dialogue")mode.SelectedIndex=0;updating=false;Render();}
    private void Edit(Func<ReadingArea,ReadingArea> change){if(updating||Selected is not {} area)return;areas[list.SelectedIndex]=change(area).Validate();saveTimer.Stop();saveTimer.Start();Render();}
    private void Save(){controller.Settings=controller.Settings with{Region=areas[0].Source,Style=controller.Settings.Style with{FontSize=areas[0].FontSize,Background=areas[0].Background,StackPrevious=false}};controller.ReadingAreas=areas.Skip(1).ToArray();note.Text="حُفظت المناطق. الحوار مستقل عن الرسائل والقوائم؛ يمكن إيقاف أي منطقة إضافية.";}
    private void RefreshFrame(){if(!IsVisible||controller.LastFrame is not {} frame||ReferenceEquals(last,frame))return;last=frame;var bmp=BitmapSource.Create(frame.Width,frame.Height,96,96,PixelFormats.Bgra32,null,frame.Pixels,frame.Stride);bmp.Freeze();image.Source=bmp;Render();}
    private void Render(){canvas.Children.Clear();canvas.Children.Add(image);foreach(var area in areas){var box=Output?area.Output:area.Source;var selected=Selected?.Id==area.Id;var color=selected?Brushes.HotPink:area.Enabled?Brushes.Turquoise:Brushes.Gray;var rectangle=new Rectangle{Width=(box.Right-box.Left)*1280,Height=(box.Bottom-box.Top)*720,Stroke=color,StrokeThickness=selected?4:2,Fill=new SolidColorBrush(Color.FromArgb(selected?(byte)22:(byte)5,220,80,180)),IsHitTestVisible=false};Canvas.SetLeft(rectangle,box.Left*1280);Canvas.SetTop(rectangle,box.Top*720);canvas.Children.Add(rectangle);var title=new TextBlock{Text=area.Name,Foreground=Brushes.White,Background=Brushes.Black,FontSize=21,IsHitTestVisible=false};Canvas.SetLeft(title,box.Left*1280);Canvas.SetTop(title,Math.Max(0,box.Top*720-30));canvas.Children.Add(title);}}
    private void Begin(Point p){if(Selected is not {} area)return;anchor=p;original=Output?area.Output:area.Source;var inside=p.X>=original.Left*1280&&p.X<=original.Right*1280&&p.Y>=original.Top*720&&p.Y<=original.Bottom*720;if(!draw&&!inside){foreach(var other in areas.AsEnumerable().Reverse()){var box=Output?other.Output:other.Source;if(p.X>=box.Left*1280&&p.X<=box.Right*1280&&p.Y>=box.Top*720&&p.Y<=box.Bottom*720){list.SelectedIndex=areas.IndexOf(other);area=other;original=box;inside=true;break;}}}if(!draw&&!inside)return;resize=!draw&&Math.Abs(p.X-original.Right*1280)<24&&Math.Abs(p.Y-original.Bottom*720)<24;dragging=true;}
    private void Move(Point p){if(Selected is not {} area)return;var dx=(p.X-anchor.X)/1280;var dy=(p.Y-anchor.Y)/720;CropRegion box;if(draw)box=new(Math.Min(anchor.Y,p.Y)/720,Math.Max(anchor.Y,p.Y)/720,Math.Min(anchor.X,p.X)/1280,Math.Max(anchor.X,p.X)/1280);else if(resize)box=original with{Right=original.Right+dx,Bottom=original.Bottom+dy};else{dx=Math.Clamp(dx,-original.Left,1-original.Right);dy=Math.Clamp(dy,-original.Top,1-original.Bottom);box=new(original.Top+dy,original.Bottom+dy,original.Left+dx,original.Right+dx);}box=ReadingArea.Bound(box);areas[list.SelectedIndex]=Output?area with{Output=box}:area with{Source=box};Render();}
    private void End(){if(!dragging)return;dragging=false;draw=false;Save();}
}
