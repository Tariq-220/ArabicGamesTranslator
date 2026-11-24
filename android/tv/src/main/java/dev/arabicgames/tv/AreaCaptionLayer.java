// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.tv;
import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Handler;
import android.os.Looper;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.TextView;
import org.json.JSONObject;
import java.util.HashMap;
import java.util.Map;

/** Independent receiver channels. Updating a menu never clears dialogue or another area.
 * Native captions avoid running a browser over HDMI on low-memory TVs. */
public final class AreaCaptionLayer extends FrameLayout {
 private final android.graphics.Typeface font;
 private final Handler handler=new Handler(Looper.getMainLooper());
 private final Map<String,TextView> views=new HashMap<>();
 private final Map<String,Long> revisions=new HashMap<>();
 private final Map<String,Runnable> expiry=new HashMap<>();
 public AreaCaptionLayer(Context context){super(context);font=android.graphics.Typeface.createFromAsset(context.getAssets(),"fonts/NotoSansArabic.ttf");setFocusable(false);setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);}
 public void apply(JSONObject data){
  String id=data.optString("id","");if(id.isEmpty()||id.length()>80)return;
  long revision=data.optLong("revision",0);if(revision<revisions.getOrDefault(id,-1L))return;
  if(!views.containsKey(id)&&views.size()>=8)return;
  revisions.put(id,revision);Runnable old=expiry.remove(id);if(old!=null)handler.removeCallbacks(old);
  if(data.optBoolean("clear",false)){remove(id);return;}
  String text=data.optString("text","");if(text.isEmpty()||text.length()>6000){remove(id);return;}
  JSONObject box=data.optJSONObject("output");if(box==null)return;
  double left=bound(box.optDouble("left",.1)),top=bound(box.optDouble("top",.1));
  double right=bound(box.optDouble("right",.9)),bottom=bound(box.optDouble("bottom",.8));
  if(right-left<.02||bottom-top<.02)return;
  TextView view=views.get(id);if(view==null){view=new TextView(getContext());view.setFocusable(false);view.setTextDirection(View.TEXT_DIRECTION_RTL);view.setGravity(Gravity.TOP|Gravity.RIGHT);view.setTextColor(Color.WHITE);view.setPadding(8,5,8,5);views.put(id,view);addView(view);}
  view.setTypeface(font);view.setText(text);view.setShadowLayer(3,0,1,Color.BLACK);
  int opacity=Math.max(0,Math.min(100,data.optInt("background",0)));GradientDrawable background=new GradientDrawable();background.setColor(Math.round(opacity*255f/100)<<24);background.setCornerRadius(8);view.setBackground(background);
  int width=Math.max(1,getWidth()),height=Math.max(1,getHeight());
  FrameLayout.LayoutParams p=new FrameLayout.LayoutParams(Math.max(40,(int)((right-left)*width)),Math.max(30,(int)((bottom-top)*height)),Gravity.TOP|Gravity.LEFT);p.leftMargin=(int)(left*width);p.topMargin=(int)(top*height);view.setLayoutParams(p);
  int font=Math.max(14,Math.min(48,data.optInt("fontSize",24)));view.setTextSize(TypedValue.COMPLEX_UNIT_PX,Math.max(14,width*font/1280f));
  view.setVisibility(View.VISIBLE);final TextView caption=view;
  Runnable hide=()->{if(revisions.getOrDefault(id,-1L)==revision)caption.setVisibility(View.INVISIBLE);expiry.remove(id);};expiry.put(id,hide);handler.postDelayed(hide,Math.max(1200,Math.min(10000,data.optInt("duration",3500))));
 }
 private static double bound(double value){return Double.isFinite(value)?Math.max(0,Math.min(1,value)):0;}
 private void remove(String id){TextView view=views.remove(id);if(view!=null)removeView(view);}
 public void clearAll(){for(Runnable r:expiry.values())handler.removeCallbacks(r);expiry.clear();views.clear();revisions.clear();removeAllViews();}
 @Override protected void onDetachedFromWindow(){clearAll();super.onDetachedFromWindow();}
}
