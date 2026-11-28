// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.view.*;
import android.widget.*;
final class Ui {
 static final int BG=Color.rgb(15,23,36),CARD=Color.rgb(29,41,58),TEXT=0xfff4f7fa,MUTED=0xffa9b8ca,ACCENT=0xff72e2b1;
 static String version(Context c){try{return c.getPackageManager().getPackageInfo(c.getPackageName(),0).versionName;}catch(android.content.pm.PackageManager.NameNotFoundException e){return "";}}
 static void fitVideoLayer(FrameLayout player,View overlay){player.addOnLayoutChangeListener((v,l,t,r,b,ol,ot,or,ob)->{int availableW=Math.max(1,r-l),availableH=Math.max(1,b-t);android.graphics.Bitmap frame=PhoneVideoPreview.latest;double aspect=frame==null?16d/9:frame.getWidth()/(double)frame.getHeight();int w=Math.min(availableW,(int)(availableH*aspect)),h=Math.min(availableH,(int)(w/aspect));ViewGroup.LayoutParams before=overlay.getLayoutParams();if(before instanceof FrameLayout.LayoutParams&&before.width==w&&before.height==h&&((FrameLayout.LayoutParams)before).gravity==Gravity.CENTER)return;FrameLayout.LayoutParams p=new FrameLayout.LayoutParams(w,h,Gravity.CENTER);overlay.setLayoutParams(p);});}
 static int dp(Context c,int n){return Math.round(n*c.getResources().getDisplayMetrics().density);}
 static LinearLayout column(Context c){LinearLayout l=new LinearLayout(c);l.setOrientation(LinearLayout.VERTICAL);l.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);return l;}
 static TextView text(Context c,String t,int sp){TextView v=new TextView(c);v.setText(t);v.setTypeface(CaptionVisual.font(c,"noto"));v.setTextColor(TEXT);v.setTextSize(sp);v.setPadding(0,dp(c,4),0,dp(c,4));v.setLineSpacing(dp(c,2),1);return v;}
 static GradientDrawable background(Context c,int color){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(c,14));return d;}
 static LinearLayout card(Context c,LinearLayout parent,String title){LinearLayout l=column(c);l.setPadding(dp(c,16),dp(c,12),dp(c,16),dp(c,12));l.setBackground(background(c,CARD));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.bottomMargin=dp(c,12);parent.addView(l,p);if(title!=null){TextView label=text(c,title,17);label.setTypeface(null,Typeface.BOLD);l.addView(label);}return l;}
 static LinearLayout fold(Context c,LinearLayout parent,String label){LinearLayout content=column(c);content.setVisibility(View.GONE);button(c,parent,label,false,()->content.setVisibility(content.getVisibility()==View.VISIBLE?View.GONE:View.VISIBLE));parent.addView(content);return content;}
 static Button button(Context c,LinearLayout parent,String label,boolean primary,Runnable action){Button b=new Button(c);b.setText(label);b.setTypeface(CaptionVisual.font(c,"noto"));b.setTextSize(15);b.setAllCaps(false);b.setMinHeight(dp(c,48));b.setPadding(dp(c,12),dp(c,8),dp(c,12),dp(c,8));b.setTextColor(primary?BG:TEXT);b.setBackground(background(c,primary?ACCENT:0xff34465f));b.setOnClickListener(v->action.run());LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.topMargin=dp(c,8);parent.addView(b,p);return b;}
}
