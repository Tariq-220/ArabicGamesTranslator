// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
namespace GameTranslator.Core;
/// <summary>Character names are terminology, not broken dialogue. Some providers deliberately
/// preserve speaker labels in Latin script. Only names actually present in the source are localized.</summary>
public static class ArabicGameNames
{
    private static readonly IReadOnlyDictionary<string,string> Names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
        ["Local"]="أحد السكان",["Locals"]="السكان",["Driver"]="السائق",["Stranger"]="الغريب",["Cop"]="الشرطي",["Tourist"]="السائح",["Tourists"]="السياح",["Officer"]="الشرطي",["Man"]="الرجل",["Woman"]="المرأة",
        ["Jason"]="جيسون",["Lucia"]="لوسيا",["Donnie"]="دوني",["Leonida"]="ليونيدا",
        ["Vicki"]="فيكي",["Kyle"]="كايل",["Raul"]="راؤول",["Dre'Quan"]="دري كوان",["Karim"]="كريم",["Boobie"]="بوبي",
        ["Wade"]="وايد",["Billy"]="بيلي",["Kenton"]="كينتون",["Andy"]="آندي",["Shirley"]="شيرلي",["Brian"]="براين",
        ["John"]="جون",["Arthur"]="آرثر",["Marston"]="مارستون",["Atreus"]="أتريوس",["Kratos"]="كريتوس",["Mimir"]="ميمير"
    };
    public static string CanonicalSpeaker(string name){var value=name.Trim();if(Names.ContainsKey(value))return value;var plain=string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);return Names.ContainsKey(plain)?plain:value;}
    public static string? Speaker(string name)=>Names.GetValueOrDefault(CanonicalSpeaker(name));
    public static string NormalizeSpeakerLabels(string source)=>Regex.Replace(source,@"(?m)^(?<label>[^:\r\n]{1,40})(?<colon>[:：])",m=>CanonicalSpeaker(m.Groups["label"].Value)+m.Groups["colon"].Value);
    public static string Localize(string source,string output)
    {
        foreach(var (name,arabic) in Names)
        {
            if(name is "Local" or "Locals" or "Driver" && !Regex.IsMatch(source,@"(?im)^\s*"+Regex.Escape(name)+@"\s*[:：]"))continue;
            var pattern=@"(?<![A-Za-z])"+Regex.Escape(name)+@"(?![A-Za-z])";
            if(Regex.IsMatch(source,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
                output=Regex.Replace(output,pattern,arabic,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
        return output;
    }
}
