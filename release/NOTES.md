# ArabicGames 0.4.24 — إصلاح مسار قراءة الحوار

- [Windows 0.4.24 — إصلاح وتحديث النسخة الحالية](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/ArabicGames-Windows-0.4.24-Update.zip)
- [Windows 0.4.24 — تثبيت أول مرة](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/ArabicGames-Windows-0.4.24-Full-Install.zip)
- [Google TV / TCL 0.3.10 — APK الشاشة](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/ArabicGames-TV-0.3.10.apk)
- [جوال Android 0.4.13 — APK الجوال](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/ArabicGames-Phone-0.4.13-arm64.apk)
- [LG webOS 1.0.8 — IPK الشاشة](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/com.gametranslator.tv_1.0.8_all.ipk)
- [Samsung 0.1.0 — حزمة مطوّر تحتاج شهادة](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.24/ArabicGames-Samsung-0.1.0-Developer-Kit.zip)

## إصلاح الجوال
- متابعة قراءة الفيديو بمعدل محدود حتى إذا تجمدت عدادات WebView، بدل توقف القراءة نهائيًا.
- تحرير حجز الإطار عند تغيير الجلسة/الإعدادات، واستعادة القراءة إذا بقي الإطار معلقًا 15 ثانية.
- زر استعادة الحوار والسرعة المعتدلة دون حذف النماذج أو المفاتيح.

## إصلاح الحوار
- إزالة الانتظار الإضافي الإلزامي وإعادة مهلة وصول النتيجة السابقة بدل إسقاطها فور اختفاء القراءة.
- استعادة منطقة الحوار مرة واحدة مع الحفاظ على النماذج والمفاتيح. زر إصلاح القراءة يعيد الإعداد السابق.
- حفظ منطقة الحوار منفصلة عن القوائم، وبدء التطبيق في الحوار؛ القوائم تُقرأ كاملة عند اختيارها صراحة.
- تحسين حفظ اسم المتحدث الملوّن بين قراءتي OCR، مقتبس من سورس Sefflex دون طلب ترجمة إضافي.
- حماية استمرارية القراءة عند وصول نص مؤلف من علامات فقط.

Google TV كما في الإصدار السابق؛ لم نضف وضع HDR التجريبي لهذا الإصدار. التصميم والمحدث محفوظان. اختبارات آلية لا تغني عن تجربة Remote Play والكونسول الفعلية.
