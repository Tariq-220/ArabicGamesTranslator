// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class SpellScoringTests
{
    private sealed class Words:ISpellDictionary
    {
        private readonly HashSet<string> words=new("you sure got all the weapons are here get in car going".Split(' '),StringComparer.OrdinalIgnoreCase);
        public bool Contains(string word)=>words.Contains(word);
    }
    [Fact] public void CompleteSourceBeatsLongCorruptWhiteTextReading()
    {
        var scoring=new SpellScoring(new Words());
        Assert.True(scoring.Score("You sure you've got all the weapons?")>scoring.Score("YOO SMe you've got all meit weapons'"));
    }
    [Fact] public void DialogueSlangAndNamesAreNotRejectedAsMisspellings()
    {
        var scoring=new SpellScoring(new Words());Assert.False(scoring.IsMisread("You gotta get in Lucia"));
    }
}
