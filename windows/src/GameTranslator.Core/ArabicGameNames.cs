// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;
/// <summary>Character names are terminology, not broken dialogue. Some providers deliberately
/// preserve speaker labels in Latin script. Only names actually present in the source are localized.</summary>
public static class ArabicGameNames
{
    private static readonly IReadOnlyDictionary<string,string> Names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
        ["Tourist"]="السائح",["Tourists"]="السياح",["Officer"]="الشرطي",["Man"]="الرجل",["Woman"]="المرأة",
        ["Jason"]="جيسون",["Lucia"]="لوسيا",["Donnie"]="دوني",["Leonida"]="ليونيدا",
        ["Vicki"]="فيكي",["Kyle"]="كايل",["Raul"]="راؤول",["Dre'Quan"]="دري كوان",["Karim"]="كريم",["Boobie"]="بوبي",
        ["Billy"]="بيلي",["Kenton"]="كينتون",["Andy"]="آندي",["Shirley"]="شيرلي",["Brian"]="براين",
        ["John"]="جون",["Arthur"]="آرثر",["Marston"]="مارستون",["Atreus"]="أتريوس",["Kratos"]="كريتوس",["Mimir"]="ميمير"
    };
    public static string? Speaker(string name)=>Names.GetValueOrDefault(name.Trim());
    public static string Localize(string source,string output)
    {
        foreach(var (name,arabic) in Names)
        {
            var pattern=@"(?<![A-Za-z])"+Regex.Escape(name)+@"(?![A-Za-z])";
            if(Regex.IsMatch(source,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
                output=Regex.Replace(output,pattern,arabic,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
        return output;
    }
}
