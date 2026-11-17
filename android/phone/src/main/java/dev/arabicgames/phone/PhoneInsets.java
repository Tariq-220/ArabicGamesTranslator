// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.graphics.Insets;
import android.os.Build;
import android.view.View;
import android.view.WindowInsets;
/** Keep controls clear of Android 15's enforced edge-to-edge system bars/cutouts. */
final class PhoneInsets {
 static void fit(View root){
  final int left=root.getPaddingLeft(),top=root.getPaddingTop(),right=root.getPaddingRight(),bottom=root.getPaddingBottom();
  root.setOnApplyWindowInsetsListener((view,window)->{
   if(Build.VERSION.SDK_INT>=30){
    Insets bars=window.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());
    view.setPadding(left+bars.left,top+bars.top,right+bars.right,bottom+bars.bottom);return WindowInsets.CONSUMED;
   }
   view.setPadding(left+window.getSystemWindowInsetLeft(),top+window.getSystemWindowInsetTop(),right+window.getSystemWindowInsetRight(),bottom+window.getSystemWindowInsetBottom());return window.consumeSystemWindowInsets();
  });
  root.requestApplyInsets();
 }
}
