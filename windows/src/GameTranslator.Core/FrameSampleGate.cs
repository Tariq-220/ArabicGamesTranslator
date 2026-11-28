// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Core;

/// <summary>Keep the requested OCR rate without losing alternate frames to capture timer jitter.
/// Deadlines advance from the schedule, not from each slightly late arrival. No frame is queued.</summary>
public sealed class FrameSampleGate
{
    private readonly object gate=new();
    private double nextAt;
    private long lastAt;
    private int rate;
    public bool Due(long at,int fps)
    {
        lock(gate){
            fps=Math.Clamp(fps,1,60);double period=1000d/fps;
            if(rate!=fps||at<lastAt){rate=fps;nextAt=at;}
            lastAt=at;
            if(at<nextAt)return false;
            nextAt=at-nextAt>period*2?at+period:nextAt+period;
            return true;
        }
    }
}
