// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;using Microsoft.Win32;using GameTranslator.Translation;
namespace GameTranslator.App.Views;
internal static class ComparisonExportDialog {
 public static async void Save(Window owner,string source,Action<string> status){var picker=new SaveFileDialog{Title="حفظ سجل المقارنة لإرساله بالمحادثة",FileName="comparison-windows.jsonl",Filter="سجل مقارنة (*.jsonl)|*.jsonl",DefaultExt=".jsonl",OverwritePrompt=true};if(picker.ShowDialog(owner)!=true)return;status("جارٍ حفظ نسخة السجل محليًا…");try{var result=await Task.Run(()=>ComparisonLogExporter.Export(source,picker.FileName));status($"تم حفظ {result.Rows} سطرًا. أرفق comparison-windows.jsonl بالمحادثة. السطور غير المكتملة المتجاهلة: {result.Skipped}.");}catch(Exception e) when(e is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or ArgumentException){status("تعذر التصدير أو لا توجد سطور محفوظة. فعّل تسجيل المقارنة وشغّل حوارًا ثم حاول مجددًا.");}}
}
