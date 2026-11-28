// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;import android.graphics.*;import android.view.*;import org.json.*;
/** Only the selected card is draggable; source and output share its colour and have different line styles. */
final class CardPreview extends View {
 private final JSONObject card;private final int color;private final Runnable changed;private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);private final RectF video=new RectF();private final float[] anchor=new float[2];private float[] original;private boolean dragging,resize;boolean output;
 CardPreview(Context context,JSONObject card,int color,Runnable changed){super(context);this.card=card;this.color=color;this.changed=changed;paint.setTypeface(CaptionVisual.font(context,"noto"));setContentDescription("معاينة البطاقة المحددة: "+card.optString("name"));}
 @Override protected void onMeasure(int width,int height){int w=MeasureSpec.getSize(width);setMeasuredDimension(w,Math.max(Ui.dp(getContext(),120),w*9/16));}
 private final Runnable refresh=new Runnable(){public void run(){invalidate();postDelayed(this,200);}};
 @Override protected void onAttachedToWindow(){super.onAttachedToWindow();post(refresh);}
 @Override protected void onDetachedFromWindow(){removeCallbacks(refresh);super.onDetachedFromWindow();}
 @Override protected void onDraw(Canvas canvas){super.onDraw(canvas);canvas.drawColor(0xff101722);Bitmap frame=PhoneVideoPreview.latest;video.set(0,0,getWidth(),getHeight());if(frame!=null&&!frame.isRecycled()){float scale=Math.min(getWidth()/(float)frame.getWidth(),getHeight()/(float)frame.getHeight()),w=frame.getWidth()*scale,h=frame.getHeight()*scale;video.set((getWidth()-w)/2,(getHeight()-h)/2,(getWidth()+w)/2,(getHeight()+h)/2);canvas.drawBitmap(frame,null,video,null);}drawBox(canvas,"source",false);drawBox(canvas,"output",true);}
 private RectF rect(float[] b){return new RectF(video.left+b[0]*video.width(),video.top+b[1]*video.height(),video.left+(b[0]+b[2])*video.width(),video.top+(b[1]+b[3])*video.height());}
 private void drawBox(Canvas canvas,String key,boolean dashed){RectF box=rect(ReadingCards.bounds(card,key));paint.setColor(color);paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(Ui.dp(getContext(),2));paint.setPathEffect(dashed?new DashPathEffect(new float[]{12,8},0):null);canvas.drawRect(box,paint);paint.setPathEffect(null);paint.setStyle(Paint.Style.FILL);paint.setTextSize(Ui.dp(getContext(),12));canvas.drawText(dashed?"الترجمة":"القراءة",box.left+4,Math.max(14,box.top-4),paint);if(dashed==output)canvas.drawCircle(box.right,box.bottom,Ui.dp(getContext(),6),paint);}
 @Override public boolean onTouchEvent(MotionEvent event){String key=output?"output":"source";float x=(event.getX()-video.left)/Math.max(1,video.width()),y=(event.getY()-video.top)/Math.max(1,video.height());switch(event.getActionMasked()){
  case MotionEvent.ACTION_DOWN:original=ReadingCards.bounds(card,key);RectF r=rect(original);resize=Math.hypot(event.getX()-r.right,event.getY()-r.bottom)<Ui.dp(getContext(),24);if(!resize&&!r.contains(event.getX(),event.getY()))return false;anchor[0]=x;anchor[1]=y;dragging=true;getParent().requestDisallowInterceptTouchEvent(true);return true;
  case MotionEvent.ACTION_MOVE:if(!dragging)return false;float dx=x-anchor[0],dy=y-anchor[1],l=original[0],t=original[1],w=original[2],h=original[3];if(resize){w=Math.max(.03f,Math.min(1-l,w+dx));h=Math.max(.03f,Math.min(1-t,h+dy));}else{l=Math.max(0,Math.min(1-w,l+dx));t=Math.max(0,Math.min(1-h,t+dy));}try{card.put(key,ReadingCards.box(l,t,l+w,t+h));}catch(JSONException ignored){}invalidate();return true;
  case MotionEvent.ACTION_UP:case MotionEvent.ACTION_CANCEL:if(!dragging)return false;dragging=false;getParent().requestDisallowInterceptTouchEvent(false);performClick();changed.run();return true;
  default:return false;}}
 @Override public boolean performClick(){super.performClick();return true;}
}
