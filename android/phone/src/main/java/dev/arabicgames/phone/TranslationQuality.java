// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
/** Lightweight warning only: cannot judge meaning or automatically approve a translation. */
final class TranslationQuality {
 static String warning(String source,String arabic){
  if(arabic==null||!arabic.matches("(?s).*\\p{IsArabic}.*"))return "الناتج لا يحتوي نصًا عربيًا؛ راجع المحرك أو صحّح السطر.";
  if(arabic.matches("(?s).*[A-Za-z]{2,}(?:[ ,]+[A-Za-z]{2,}){2,}.*"))return "قد تكون جملة إنجليزية لم تُترجم؛ راجعها في السجل.";
  String en=source==null?"":source.toLowerCase(java.util.Locale.ROOT);
  if(en.contains("kill the engine")&&arabic.contains("اقتل المحرك"))return "ترجمة حرفية محتملة: المقصود إطفاء المحرك. راجع السطر قبل اعتماده.";
  if(en.matches("(?s).*out(?:ta| of) your hair.*")&&arabic.contains("شعرك"))return "قد يكون تعبيرًا بمعنى ترك الشخص وشأنه، وليس الشعر حرفيًا.";
  if(en.contains("roll of bills")&&arabic.contains("فواتير"))return "bills هنا قد تعني نقودًا، لا فواتير؛ راجع السياق.";
  if(en.contains("look the part")&&arabic.contains("تنظر"))return "قد يكون المقصود المظهر المناسب، لا النظر إلى جزء.";
  return "";
 }
}
