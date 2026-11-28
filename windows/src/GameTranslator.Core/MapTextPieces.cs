// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.RegularExpressions;
namespace GameTranslator.Core;

/// <summary>A plain map caption still has independent titles, prose and fields.
/// Keep visual wraps inside their paragraph; one rejected title must not hide the description.</summary>
public static class MapTextPieces
{
    public static string[] Split(string source)
    {
        var result=new List<string>();var paragraph=new List<string>();
        void Flush(){if(paragraph.Count>0){result.Add(string.Join(' ',paragraph));paragraph.Clear();}}
        foreach(var raw in source.Replace("\r\n","\n").Split('\n')){
            var row=raw.Trim();if(row.Length==0){Flush();continue;}
            // The OCR J/n are the two recorded mission icons, not part of the sentence.
            if(row.Equals("J Jason only",StringComparison.OrdinalIgnoreCase))row="Jason only";
            if(Regex.IsMatch(row,@"^n MEET DRE'QU[ÅA]N$",RegexOptions.IgnoreCase))row=row[2..];
            bool heading=row.Any(char.IsLetter)&&row.Where(char.IsLetter).All(char.IsUpper);
            bool eligibility=Regex.IsMatch(row,@"^(?:Jason|Lucia) only$",RegexOptions.IgnoreCase);
            if(heading||eligibility){Flush();result.Add(row);}
            else{paragraph.Add(row);if(row.EndsWith('.')||row.EndsWith('!')||row.EndsWith('?'))Flush();}
        }
        Flush();return result.ToArray();
    }
}
