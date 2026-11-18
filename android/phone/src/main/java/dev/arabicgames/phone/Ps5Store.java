// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.*;
import android.security.keystore.*;
import android.util.Base64;
import java.security.*;
import javax.crypto.*;
import javax.crypto.spec.GCMParameterSpec;
import org.json.*;

/** Separate encrypted PS5 registration; does not modify translator keys. */
final class Ps5Store {
 private static final String ALIAS="ArabicGamesPs5Pairing";
 static final class State {
  final long revision;final byte[] account,key,morning;final String host,name;
  State(long rev,byte[] a,String h,String n,byte[] k,byte[] m){revision=rev;account=a;host=h;name=n;key=k;morning=m;}
  boolean paired(){return account!=null&&key!=null&&morning!=null&&dev.arabicgames.Pairing.privateIpv4(host);}
 }
 private static SharedPreferences prefs(Context c){return c.getSharedPreferences("ps5-native",Context.MODE_PRIVATE);}
 private static SecretKey key()throws Exception{KeyStore s=KeyStore.getInstance("AndroidKeyStore");s.load(null);if(s.containsAlias(ALIAS))return(SecretKey)s.getKey(ALIAS,null);KeyGenerator g=KeyGenerator.getInstance("AES","AndroidKeyStore");g.init(new KeyGenParameterSpec.Builder(ALIAS,KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT).setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());return g.generateKey();}
 static synchronized State read(Context c)throws Exception{
  SharedPreferences p=prefs(c);long rev=p.getLong("revision",0);String stored=p.getString("sealed","");if(stored.isEmpty())return new State(rev,null,"","",null,null);
  String[] pair=stored.split(":",-1);if(pair.length!=2)throw new GeneralSecurityException();Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.DECRYPT_MODE,key(),new GCMParameterSpec(128,Base64.decode(pair[0],Base64.NO_WRAP)));
  JSONObject j=new JSONObject(new String(cipher.doFinal(Base64.decode(pair[1],Base64.NO_WRAP)),java.nio.charset.StandardCharsets.UTF_8));
  byte[] account=decode(j.optString("account"),8),k=decode(j.optString("key"),16),m=decode(j.optString("morning"),16);
  if(account==null)throw new GeneralSecurityException();return new State(rev,account,j.optString("host"),j.optString("name"),k,m);
 }
 private static byte[] decode(String s,int size){if(s.isEmpty())return null;try{byte[] b=Base64.decode(s,Base64.NO_WRAP);return b.length==size?b:null;}catch(Exception e){return null;}}
 private static void write(Context c,State state)throws Exception{
  JSONObject j=new JSONObject().put("account",Base64.encodeToString(state.account,Base64.NO_WRAP));if(state.paired())j.put("host",state.host).put("name",state.name).put("key",Base64.encodeToString(state.key,Base64.NO_WRAP)).put("morning",Base64.encodeToString(state.morning,Base64.NO_WRAP));
  Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,key());String value=Base64.encodeToString(cipher.getIV(),Base64.NO_WRAP)+":"+Base64.encodeToString(cipher.doFinal(j.toString().getBytes(java.nio.charset.StandardCharsets.UTF_8)),Base64.NO_WRAP);
  if(!prefs(c).edit().putString("sealed",value).putLong("revision",state.revision).commit())throw new java.io.IOException("Storage unavailable");
 }
 static synchronized void account(Context c,byte[] id)throws Exception{if(id==null||id.length!=8)throw new IllegalArgumentException();State old=read(c);boolean same=java.util.Arrays.equals(old.account,id);write(c,new State(old.revision+1,id.clone(),same?old.host:"",same?old.name:"",same?old.key:null,same?old.morning:null));}
 static synchronized void paired(Context c,long revision,String host,String name,byte[] registered)throws Exception{
  if(!dev.arabicgames.Pairing.privateIpv4(host)||registered.length!=64)throw new IllegalArgumentException();State old=read(c);if(old.revision!=revision||old.account==null)throw new IllegalStateException("Setup changed");
  write(c,new State(revision+1,old.account,host,name.substring(0,Math.min(100,name.length())),java.util.Arrays.copyOfRange(registered,32,48),java.util.Arrays.copyOfRange(registered,48,64)));
 }
 static synchronized void clear(Context c){SharedPreferences p=prefs(c);p.edit().remove("sealed").putLong("revision",p.getLong("revision",0)+1).commit();}
}
