// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>The configured interval includes capture work, rather than being added after it.</summary>
public static class CaptureCadence
{
    public static int RemainingMs(int intervalMs,long workMs)=>(int)Math.Max(1,Math.Clamp(intervalMs,16,1000)-Math.Clamp(workMs,0,int.MaxValue));
}
