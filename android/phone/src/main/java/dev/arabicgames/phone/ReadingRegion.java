// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
final class ReadingRegion {
 static float[] bounds(String mode){switch(mode){case "all":return new float[]{0,0,1,1};case "left":return new float[]{0,0,.65f,1};case "right":return new float[]{.35f,0,.65f,1};default:return new float[]{.10f,.62f,.80f,.35f};}}
 static String sourceLimit(String text){if(text.length()<=1100)return text;int end=text.lastIndexOf('\n',1100);if(end<100)end=text.lastIndexOf(' ',1100);return text.substring(0,end>0?end:1100);}
}
