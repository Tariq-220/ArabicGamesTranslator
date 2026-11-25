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
 private final Map<String,JSONObject> pending=new HashMap<>();
 private final Map<String,View> views=new HashMap<>();
 private final Map<String,String> texts=new HashMap<>();
 private final Map<String,Long> revisions=new HashMap<>();
 private final Map<String,Runnable> expiry=new HashMap<>();
 public AreaCaptionLayer(Context context){super(context);font=android.graphics.Typeface.createFromAsset(context.getAssets(),"fonts/NotoSansArabic.ttf");setFocusable(false);setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);}
 public void apply(JSONObject data){
  String id=data.optString("id","");if(id.isEmpty()||id.length()>80)return;
  if(getWidth()==0||getHeight()==0){if(data.optBoolean("clear",false))pending.remove(id);else if(pending.size()<8||pending.containsKey(id))pending.put(id,data);return;}
  long revision=data.optLong("revision",0);if(revision<revisions.getOrDefault(id,-1L))return;
  if(!views.containsKey(id)&&views.size()>=8)return;
  revisions.put(id,revision);Runnable old=expiry.remove(id);if(old!=null)handler.removeCallbacks(old);
  if(data.optBoolean("clear",false)){remove(id);return;}
  String text=data.optString("text","");if(text.isEmpty()||text.length()>6000){remove(id);return;}
  JSONObject box=data.optJSONObject("output");if(box==null)return;
  double left=bound(box.optDouble("left",.1)),top=bound(box.optDouble("top",.1));
  double right=bound(box.optDouble("right",.9)),bottom=bound(box.optDouble("bottom",.8));
  if(right-left<.02||bottom-top<.02)return;
  View oldView=views.get(id);if(oldView!=null)removeView(oldView);
  JSONObject panel=data.optJSONObject("panel"),appearance=data.optJSONObject("appearance");
  int screenWidth=Math.max(1,getWidth()),screenHeight=Math.max(1,getHeight());
  int outWidth=Math.max(40,(int)((right-left)*screenWidth)),outHeight=Math.max(30,(int)((bottom-top)*screenHeight));
  View view;
  if(hasCells(panel))view=panel(panel,appearance,outWidth,outHeight,data.optInt("fontSize",24),data.optInt("background",0));
  else{TextView label=new TextView(getContext());CaptionVisual.apply(label,appearance,data.optInt("background",0),Math.max(14,screenWidth*data.optInt("fontSize",24)/1920f));label.setText(CaptionVisual.speaker(text,appearance));label.setMaxWidth(outWidth);view=label;}
  view.setFocusable(false);views.put(id,view);addView(view);
  
  int width=Math.max(1,getWidth()),height=Math.max(1,getHeight());
  FrameLayout.LayoutParams p=new FrameLayout.LayoutParams(Math.max(40,(int)((right-left)*width)),Math.max(30,(int)((bottom-top)*height)),Gravity.TOP|Gravity.LEFT);p.leftMargin=(int)(left*width);p.topMargin=(int)(top*height);view.setLayoutParams(p);
  
  view.setVisibility(View.VISIBLE);boolean changed=!text.equals(texts.put(id,text));int fade=appearance==null?0:Math.max(0,Math.min(300,appearance.optInt("fadeMs",100)));if(changed&&fade>0&&(oldView==null||oldView.getVisibility()!=View.VISIBLE)){view.setAlpha(0);view.animate().alpha(1).setDuration(fade).start();}final View caption=view;
  Runnable hide=()->{if(revisions.getOrDefault(id,-1L)==revision)caption.setVisibility(View.INVISIBLE);expiry.remove(id);};expiry.put(id,hide);handler.postDelayed(hide,Math.max(1200,Math.min(30000,data.optInt("duration",3500))));
 }

 private static boolean hasCells(JSONObject panel){if(panel==null||panel.optJSONArray("cells")==null)return false;org.json.JSONArray cells=panel.optJSONArray("cells");for(int i=0;i<Math.min(24,cells.length());i++){JSONObject c=cells.optJSONObject(i);if(c==null||c.optString("text","").trim().isEmpty())continue;JSONObject b=c.optJSONObject("box");if(b!=null&&b.optDouble("right")>b.optDouble("left")&&b.optDouble("bottom")>b.optDouble("top"))return true;}return false;}
 @Override protected void onSizeChanged(int w,int h,int oldw,int oldh){super.onSizeChanged(w,h,oldw,oldh);if(w>0&&h>0&&!pending.isEmpty()){java.util.List<JSONObject> values=new java.util.ArrayList<>(pending.values());pending.clear();for(JSONObject value:values)apply(value);}}
 private View panel(JSONObject panel,JSONObject appearance,int width,int height,int fontSize,int opacity){
  if("menu".equals(panel.optString("kind"))){try{appearance=appearance==null?new JSONObject():new JSONObject(appearance.toString());appearance.put("backdrop","none");appearance.put("outline",true);}catch(org.json.JSONException ignored){}}
  FrameLayout outer=new FrameLayout(getContext());outer.setClipChildren(true);
  int pw=Math.max(1,Math.min(4096,panel.optInt("width",360))),ph=Math.max(1,Math.min(4096,panel.optInt("height",700)));
  float scale=Math.min((float)width/pw,(float)height/ph);int w=Math.max(1,(int)(pw*scale)),h=Math.max(1,(int)(ph*scale));
  FrameLayout canvas=new FrameLayout(getContext());canvas.setClipChildren(true);FrameLayout.LayoutParams cp=new FrameLayout.LayoutParams(w,h,Gravity.TOP|Gravity.CENTER_HORIZONTAL);outer.addView(canvas,cp);
  boolean phone="phone".equals(panel.optString("kind"))||"notification".equals(panel.optString("kind"));boolean floating="phone".equals(panel.optString("kind"))&&appearance!=null&&"floating".equals(appearance.optString("phoneStyle"));if(phone&&!floating)canvas.setBackgroundColor(Color.WHITE);
  String encoded=panel.optString("image","");if(encoded.length()>0&&encoded.length()<1600000){try{byte[] bytes=android.util.Base64.decode(encoded,android.util.Base64.DEFAULT);android.graphics.BitmapFactory.Options options=new android.graphics.BitmapFactory.Options();options.inJustDecodeBounds=true;android.graphics.BitmapFactory.decodeByteArray(bytes,0,bytes.length,options);if(options.outWidth>0&&options.outWidth<=360&&options.outHeight>0&&options.outHeight<=760){android.graphics.Bitmap bmp=android.graphics.BitmapFactory.decodeByteArray(bytes,0,bytes.length);android.widget.ImageView image=new android.widget.ImageView(getContext());image.setImageBitmap(bmp);image.setScaleType(android.widget.ImageView.ScaleType.FIT_XY);canvas.addView(image,new FrameLayout.LayoutParams(w,h));}}catch(IllegalArgumentException ignored){}}
  org.json.JSONArray cells=panel.optJSONArray("cells");for(int i=0;i<Math.min(24,cells.length());i++){
   JSONObject cell=cells.optJSONObject(i);if(cell==null)continue;String text=cell.optString("text","");if(text.length()==0||text.length()>2000)continue;
   JSONObject box=cell.optJSONObject("box");if(box==null)continue;double l=bound(box.optDouble("left")),t=bound(box.optDouble("top")),r=bound(box.optDouble("right")),b=bound(box.optDouble("bottom"));if(r<=l||b<=t)continue;
   int cw=Math.max(8,(int)((r-l)*w)),ch=Math.max(8,(int)((b-t)*h));TextView label=new TextView(getContext());label.setText(text);label.setTextDirection(View.TEXT_DIRECTION_RTL);String alignment=appearance==null?"right":appearance.optString("align","right"),vertical=appearance==null?"top":appearance.optString("vertical","top");label.setGravity(("center".equals(alignment)?Gravity.CENTER_HORIZONTAL:"left".equals(alignment)?Gravity.LEFT:Gravity.RIGHT)|(phone||"center".equals(vertical)?Gravity.CENTER_VERTICAL:"bottom".equals(vertical)?Gravity.BOTTOM:Gravity.TOP));label.setTypeface(CaptionVisual.font(getContext(),appearance==null?"noto":appearance.optString("font","noto")),cell.optBoolean("heading")||phone?android.graphics.Typeface.BOLD:android.graphics.Typeface.NORMAL);
   label.setTextColor(phone||appearance!=null&&"source".equals(appearance.optString("backdrop"))?CaptionVisual.color(cell.optString("foreground"),Color.BLACK):Color.WHITE);
   GradientDrawable bg=new GradientDrawable();bg.setColor(phone||appearance!=null&&"source".equals(appearance.optString("backdrop"))?CaptionVisual.color(cell.optString("background"),Color.WHITE):appearance!=null&&"none".equals(appearance.optString("backdrop"))?Color.TRANSPARENT:Math.max(0,Math.min(100,opacity))*255/100<<24);if(floating){bg.setAlpha(225);if(cell.optBoolean("heading")){bg.setColor(0xc8080a10);label.setTextColor(Color.WHITE);}}bg.setCornerRadius(cell.optBoolean("bubble")?Math.min(8,ch*.15f):0);label.setBackground(phone?bg:CaptionVisual.background(appearance,bg,opacity,16));label.setPadding(phone?4:2,phone?2:0,phone?4:2,phone?2:0);label.setIncludeFontPadding(false);
   int max=(int)((appearance==null||appearance.optBoolean("matchSize",true)?Math.max(8,cell.optDouble("lineHeight",.03)*ph*("notification".equals(panel.optString("kind"))?1.12:1.0)):fontSize*Math.max(1,getWidth())/1920f/scale)*scale);
   label.setAutoSizeTextTypeUniformWithConfiguration(Math.min(8,Math.max(4,max)),Math.max(8,max),1,TypedValue.COMPLEX_UNIT_PX);label.setMaxHeight(ch);if(!phone)label.setShadowLayer(2,0,1,Color.BLACK);
   FrameLayout.LayoutParams p=new FrameLayout.LayoutParams(cw,ch,Gravity.TOP|Gravity.LEFT);p.leftMargin=(int)(l*w);p.topMargin=(int)(t*h);canvas.addView(label,p);
  }
  return outer;
 }
 private static double bound(double value){return Double.isFinite(value)?Math.max(0,Math.min(1,value)):0;}
 private void remove(String id){texts.remove(id);View view=views.remove(id);if(view!=null)removeView(view);}
 public void clearAll(){pending.clear();for(Runnable r:expiry.values())handler.removeCallbacks(r);expiry.clear();views.clear();texts.clear();revisions.clear();removeAllViews();}
 @Override protected void onDetachedFromWindow(){clearAll();super.onDetachedFromWindow();}
}
