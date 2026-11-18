// SPDX-License-Identifier: Apache-2.0
// JNI ABI from Sal7one/Hearth, pinned in native-build-info.json.
package com.sal7one.common_jni.translation;
public final class Ct2Native {
 public static final Ct2Native INSTANCE=new Ct2Native();
 static {System.loadLibrary("transiber_ct2");}
 private Ct2Native(){}
 public native int protocolVersion();
 public native long create(byte[] path,int mode);
 public native byte[] translate(long handle,byte[] text,byte[] target);
 public native void cancel(long handle);
 public native void destroy(long handle);
}
