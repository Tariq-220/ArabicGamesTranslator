// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using GameTranslator.Translation;
namespace GameTranslator.App.Views;

public sealed class LiveTranslationLogWindow:Window
{
    private sealed record Row(ComparisonEntry Entry){public override string ToString(){var e=Entry;return $"{e.Time.ToLocalTime():HH:mm:ss} · {e.AreaName??"الحوار"} · {e.Engine} · {State(e.Method)}{(e.ElapsedMs is >0?$" · {e.ElapsedMs} ms":"")}\n{e.English}\n{e.Arabic}{(string.IsNullOrWhiteSpace(e.Error)?"":"\n"+e.Error)}";}}
    private readonly ComparisonLogTail tail;
    private readonly List<Row> rows=[];
    private readonly ListBox list=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TextBox search=new(){MinHeight=38,Margin=new Thickness(4)};
    private readonly TextBlock status=new(){Margin=new Thickness(5),TextWrapping=TextWrapping.Wrap};
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private bool polling,closed;
    public LiveTranslationLogWindow(string path)
    {
        tail=new(path);Title="سجل النصوص المباشر";Width=850;Height=700;MinWidth=500;MinHeight=400;FlowDirection=FlowDirection.RightToLeft;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/GameTranslator;component/Theme/ArabicTestStyles.xaml",UriKind.Relative)});
        Background=new SolidColorBrush(Color.FromRgb(18,24,35));Foreground=Brushes.White;
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        top.Children.Add(new TextBlock{Text="الحوار والجوال والقوائم — المقروء والترجمة والحالة",FontSize=21,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)});
        top.Children.Add(new TextBlock{Text="بحث في آخر 400 سجل محلي. تُضاف النتائج أثناء اللعب؛ تفعيل تسجيل المقارنة مطلوب.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)});top.Children.Add(search);
        var actions=new WrapPanel();top.Children.Add(actions);
        void Button(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(4),Padding=new Thickness(10,6,10,6),MinHeight=40};b.Click+=(_,_)=>action();actions.Children.Add(b);}
        Button("نسخ السطر المحدد",()=>{if(list.SelectedItem is Row row)Clipboard.SetText(row.ToString());});
        Button("نسخ النتائج الظاهرة",()=>Clipboard.SetText(string.Join("\n\n",list.Items.Cast<Row>())));
        Button("تصدير JSONL",()=>ComparisonExportDialog.Save(this,path,text=>status.Text=text));
        Button("تحديث",()=>Poll());top.Children.Add(status);
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding());text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);text.SetValue(TextBlock.MarginProperty,new Thickness(8));list.ItemTemplate=new DataTemplate{VisualTree=text};
        ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);VirtualizingPanel.SetIsVirtualizing(list,true);root.Children.Add(list);
        search.TextChanged+=(_,_)=>Render();timer.Tick+=(_,_)=>Poll();Loaded+=(_,_)=>{Poll();timer.Start();};Closed+=(_,_)=>{closed=true;timer.Stop();};
    }
    private async void Poll(){if(polling||closed)return;polling=true;try{var added=await Task.Run(tail.ReadNew);if(closed)return;if(added.Count>0){rows.AddRange(added.Select(e=>new Row(e)));if(rows.Count>400)rows.RemoveRange(0,rows.Count-400);Render();}status.Text=$"{rows.Count} سجل · التحديث تلقائي كل ثانية";}catch(System.IO.IOException){status.Text="السجل مشغول؛ ستتم إعادة القراءة تلقائيًا.";}catch(UnauthorizedAccessException){status.Text="تعذرت قراءة السجل المحلي.";}finally{polling=false;}}
    private void Render(){var selected=(list.SelectedItem as Row)?.Entry;var query=search.Text.Trim();list.ItemsSource=rows.AsEnumerable().Reverse().Where(r=>query.Length==0||r.ToString().Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();if(selected is not null)list.SelectedItem=rows.LastOrDefault(r=>r.Entry==selected);}
    private static string State(string? stage)=>stage switch{"translating"=>"قُرئ · ينتظر المحرك","translated"=>"تُرجمت القطعة","partial"=>"عرض جزئي","shown"=>"معروض","rejected_output"=>"المحرك لم يُعد عربية صالحة","failed"=>"تعذر الطلب","cleared"=>"انتهى النص","discarded"=>"مستبعد","rejected_untranslated"=>"إنجليزي غير مترجم",_=>"مترجم"};
}
