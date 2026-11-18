// SPDX-License-Identifier: AGPL-3.0-only
// JNI contract adapted from Sefflex/BlackBox Native.kt, AGPL-3.0-only.
package dev.arabicgames.phone.ps5;
import java.nio.ByteBuffer;
public final class NativeBridge {
 public static final NativeBridge INSTANCE=new NativeBridge();
 private static Boolean loaded;
 private NativeBridge(){}
 public static synchronized boolean available(){if(loaded!=null)return loaded;try{System.loadLibrary("gtnative");loaded=INSTANCE.init()==0;}catch(UnsatisfiedLinkError|SecurityException e){loaded=false;}return loaded;}
 public interface ConsoleCallback{void onConsole(String host,byte[] name,boolean awake);}
 public interface LogCallback{void onLog(int level,String message);}
 public interface SessionListener extends LogCallback{void onFrame(ByteBuffer buffer,int width,int height,int stride);void onEvent(int event,int reason,String detail);}
 public native int init();
 public native int discover(String[] targets,int timeoutMs,ConsoleCallback callback);
 public native int wake(String host,long credential);
 public native int register(String host,byte[] accountId,int pin,byte[] out,int[] reason,LogCallback log);
 public native long sessionStart(String host,byte[] registKey,byte[] morning,byte[] accountId,int maxFps,SessionListener listener);
 public native void sessionStop(long handle);
}
