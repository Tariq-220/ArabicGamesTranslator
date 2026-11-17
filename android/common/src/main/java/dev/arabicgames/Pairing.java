// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames;
import java.security.SecureRandom;
public final class Pairing {
 public static String createCode() { return String.format(java.util.Locale.ROOT, "%08d", new SecureRandom().nextInt(100_000_000)); }
 public static boolean privateIpv4(String host) {
  String[] parts = host.split("\\.", -1); if (parts.length != 4) return false;
  int[] n = new int[4];
  try { for (int i=0;i<4;i++) { if (!parts[i].matches("[0-9]{1,3}")) return false; n[i]=Integer.parseInt(parts[i]); if(n[i]>255)return false; } } catch (NumberFormatException e) { return false; }
  return n[0]==10 || n[0]==192 && n[1]==168 || n[0]==172 && n[1]>=16 && n[1]<=31;
 }
 public static boolean validCode(String code) { return code.matches("[0-9]{8}"); }
}
