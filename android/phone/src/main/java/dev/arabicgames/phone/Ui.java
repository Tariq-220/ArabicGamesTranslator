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
 static int dp(Context c,int n){return Math.round(n*c.getResources().getDisplayMetrics().density);}
 static LinearLayout column(Context c){LinearLayout l=new LinearLayout(c);l.setOrientation(LinearLayout.VERTICAL);l.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);return l;}
 static TextView text(Context c,String t,int sp){TextView v=new TextView(c);v.setText(t);v.setTextColor(TEXT);v.setTextSize(sp);v.setPadding(0,dp(c,4),0,dp(c,4));v.setLineSpacing(dp(c,2),1);return v;}
 static GradientDrawable background(Context c,int color){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(c,14));return d;}
 static LinearLayout card(Context c,LinearLayout parent,String title){LinearLayout l=column(c);l.setPadding(dp(c,16),dp(c,12),dp(c,16),dp(c,12));l.setBackground(background(c,CARD));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.bottomMargin=dp(c,12);parent.addView(l,p);if(title!=null){TextView label=text(c,title,17);label.setTypeface(null,Typeface.BOLD);l.addView(label);}return l;}
 static Button button(Context c,LinearLayout parent,String label,boolean primary,Runnable action){Button b=new Button(c);b.setText(label);b.setTextSize(15);b.setAllCaps(false);b.setMinHeight(dp(c,48));b.setPadding(dp(c,12),dp(c,8),dp(c,12),dp(c,8));b.setTextColor(primary?BG:TEXT);b.setBackground(background(c,primary?ACCENT:0xff34465f));b.setOnClickListener(v->action.run());LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.topMargin=dp(c,8);parent.addView(b,p);return b;}
}
