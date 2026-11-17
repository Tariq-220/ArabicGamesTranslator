// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
/** Lightweight warning only: cannot judge meaning or automatically approve a translation. */
final class TranslationQuality {
 static String warning(String source,String arabic){
  if(arabic==null||!arabic.matches("(?s).*\\p{IsArabic}.*"))return "الناتج لا يحتوي نصًا عربيًا؛ راجع المحرك أو صحّح السطر.";
  if(arabic.matches("(?s).*[A-Za-z]{2,}(?:[ ,]+[A-Za-z]{2,}){2,}.*"))return "قد تكون جملة إنجليزية لم تُترجم؛ راجعها في السجل.";
  return "";
 }
}
