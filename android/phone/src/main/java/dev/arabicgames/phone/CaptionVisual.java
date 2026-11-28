// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.style.ForegroundColorSpan;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.TextView;
import org.json.JSONObject;
import java.util.HashMap;
import java.util.Map;

/** Shared font/backdrop identifiers with Windows; no HTML, scripts or remote fonts. */
final class CaptionVisual {
 private static final Map<String,Typeface> fonts=new HashMap<>();
 static Typeface font(Context c,String id){
  String file;switch(id){case "tajawal":file="Tajawal-Regular.ttf";break;case "cairo":file="Cairo.ttf";break;case "changa":file="Changa.ttf";break;case "lalezar":file="Lalezar-Regular.ttf";break;default:file="NotoSansArabic.ttf";}
  Typeface f=fonts.get(file);if(f==null){f=Typeface.createFromAsset(c.getAssets(),"fonts/"+file);fonts.put(file,f);}return f;
 }
 static int color(String value,int fallback){return value!=null&&value.matches("#[0-9a-fA-F]{6}")?Color.parseColor(value):fallback;}
 static void apply(TextView view,JSONObject a,int opacity,float size){
  if(a==null)a=new JSONObject();Typeface base=font(view.getContext(),a.optString("font","noto"));view.setTypeface(android.os.Build.VERSION.SDK_INT>=28?Typeface.create(base,500,false):Typeface.create(base,Typeface.NORMAL));
  view.setTextSize(TypedValue.COMPLEX_UNIT_PX,size);view.setTextDirection(View.TEXT_DIRECTION_RTL);view.setTextColor(Color.WHITE);
  String align=a.optString("align","center");view.setGravity(("right".equals(align)?Gravity.RIGHT:"left".equals(align)?Gravity.LEFT:Gravity.CENTER_HORIZONTAL)|Gravity.CENTER_VERTICAL);
  String back=a.optString("backdrop","game");GradientDrawable bg=new GradientDrawable();bg.setColor("none".equals(back)?Color.TRANSPARENT:(Math.max(0,Math.min(100,opacity))*255/100<<24)|0x080a10);bg.setCornerRadius("rounded".equals(back)?size*.22f:0);view.setBackground(background(a,bg,opacity,size));
  view.setPadding((int)(size*.45),(int)(size*.12),(int)(size*.45),(int)(size*.18));
  view.setShadowLayer(a.optBoolean("outline",true)?2.5f:0,0,1,Color.BLACK);
  view.setLineSpacing(0,1.12f);view.setIncludeFontPadding(false);
 }
 static android.graphics.drawable.Drawable background(JSONObject a,GradientDrawable inner,int opacity,float size){
  String back=a==null?"game":a.optString("backdrop","game");
  if("game".equals(back)){inner.setColor((Math.max(0,Math.min(100,opacity))*255/100<<24)|0x16243e);}
  if(a==null||!a.optBoolean("accent",false))return inner;
  if("none".equals(back))inner.setColor((Math.max(0,Math.min(100,opacity))*255/100<<24)|0x0a0614);
  GradientDrawable outer=new GradientDrawable(GradientDrawable.Orientation.LEFT_RIGHT,new int[]{0xffff2d95,0xffff7a45,0xffffc94a,0xffb45cff,0xff2fd6ff});
  int alpha=Math.max(0,Math.min(100,a.optInt("accentOpacity",100)))*255/100;float strength=Math.max(0,Math.min(100,a.optInt("accentStrength",100)))/100f;
  int[] colors={0xffff2d95,0xffff7a45,0xffffc94a,0xffb45cff,0xff2fd6ff};for(int i=0;i<colors.length;i++){int c=colors[i],g=(Color.red(c)+Color.green(c)+Color.blue(c))/3;colors[i]=Color.argb(alpha,(int)(g+(Color.red(c)-g)*strength),(int)(g+(Color.green(c)-g)*strength),(int)(g+(Color.blue(c)-g)*strength));}outer.setColors(colors);
  android.graphics.drawable.LayerDrawable result=new android.graphics.drawable.LayerDrawable(new android.graphics.drawable.Drawable[]{outer,inner});int inset=Math.max(2,Math.round(size/16));result.setLayerInset(1,inset,inset,inset,inset);return result;
 }
 static CharSequence speaker(String text,JSONObject a){
  SpannableString s=new SpannableString(text);int offset=0;for(String row:text.split("\n",-1)){
  int end=row.indexOf(':');if(end<1||end>45){offset+=row.length()+1;continue;}
  String value=a==null?"auto":a.optString("speakerColor","auto");int color=Color.WHITE;
  if(a==null||a.optBoolean("speakerColors",true)){
   if("auto".equals(value)){String name=row.substring(0,end).trim();color=name.equals("جيسون")||name.equals("جايسون")?0xfff2d87a:name.equals("لوسيا")?0xfff39aba:0xff70dde7;}else color=color(value,Color.WHITE);
  }
  s.setSpan(new ForegroundColorSpan(color),offset,offset+end+1,Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);offset+=row.length()+1;}return s;
 }
}
