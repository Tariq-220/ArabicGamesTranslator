// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
/** Frame pacing and stability affect capture overhead, not the model's inference speed. */
final class LocalTiming {
 static int clamp(int value,int min,int max){return Math.max(min,Math.min(max,value));}
 static int interval(Context c){return clamp(AiPreferences.prefs(c).getInt("localFrameMs",150),100,600);}
 static int settle(Context c){return clamp(AiPreferences.prefs(c).getInt("localSettleMs",80),0,400);}
 static int maxAge(Context c){return clamp(AiPreferences.prefs(c).getInt("localMaxAgeMs",4500),1000,6000);}
 static void save(Context c,int frame,int settle,int maxAge){AiPreferences.prefs(c).edit().putInt("localFrameMs",clamp(frame,100,600)).putInt("localSettleMs",clamp(settle,0,400)).putInt("localMaxAgeMs",clamp(maxAge,1000,6000)).apply();}
}
