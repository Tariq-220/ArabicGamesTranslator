# ArabicGames 0.4.19 — إصدار موحّد ودليل عربي مصوّر

## اختر ملف جهازك

- **Windows — أول تثبيت:** [Full Install](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-Windows-0.4.19-Full-Install.zip)
- **Windows — تحديث النسخة الموجودة:** [Update](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-Windows-0.4.19-Update.zip)
- **جوال Android arm64:** [Phone 0.4.11 APK](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-Phone-0.4.11-arm64.apk)
- **شاشة Google TV / Android TV / TCL:** [TV 0.3.8 APK — ملف الشاشة](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-TV-0.3.8.apk)
- **LG webOS:** [LG 1.0.8 IPK](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/com.gametranslator.tv_1.0.8_all.ipk)
- **Samsung Tizen — نسخة مطوّر تجريبية:** [Developer Kit 0.1.0](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-Samsung-0.1.0-Developer-Kit.zip). تحتاج Developer Mode وشهادة Samsung تتضمن DUID شاشتك. ملف WGT غير موقّع ولا يثبت مباشرة. لم يُختبر على Samsung فعلية ولا يوفر OTA تلقائيًا بعد.
- **الشرح بالعربي مع صور الواجهة:** [الدليل HTML للاستخدام دون إنترنت](https://github.com/Tariq-220/ArabicGamesTranslator/releases/download/v0.4.19/ArabicGames-Arabic-Guide.zip) — فك الضغط ثم افتح index.html. الصور من تشغيل الواجهة بوضع العرض التجريبي، وليست جلسات كونسول فعلية.

## التفعيل المختصر

1. افتح مصدر اللعبة: Xbox Remote Play أو PS5 بالمسار الموضح في تبويب المصدر. اختر نافذة الفيديو الصحيحة أو الاقتران المباشر، وانتظر الصورة.
2. اختر اللعبة الصحيحة، ثم المحرك. المحلي: شغّله وانتظر Ready. السحابي: احفظ مفتاح API واختبره.
3. افتح تطبيق الشاشة المناسب على الشبكة نفسها، واختر HDMI. اختبار العربية يفحص الربط فقط؛ بعده اضغط **▶ بدء الترجمة** لقراءة اللعبة.
4. أخفِ لوحة الشاشة أثناء اللعب. أبقِ المصدر وخادم النموذج المحلي مفتوحين. تصغير المترجم بعد البدء أصبح اختيارًا واضحًا بدل التصغير التلقائي.

## الجديد

- بدء/إيقاف الترجمة ثابت وواضح بالأعلى؛ زر اختبار الشاشة داخل تبويب الربط. شريط تمرير ظاهر مع تنبيه وزر «بقية الخيارات»؛ خيارات المنطقة والسجل ظاهرة بدل إخفائها افتراضيًا.
- أنماط مستقلة للحوار، والقوائم والخريطة، ورسائل هاتف اللعبة يمينًا أو يسارًا. نمط القوائم والهاتف يتجاوز فلتر أزرار الحوار. يترجم النص الظاهر في شريط مستقل؛ لا يستبدل ملفات اللعبة أو عناصرها.
- الجوال: نفس مناطق الهاتف، وتغيير المنطقة دون إعادة تحميل أوزان النموذج المحلي. ارجع إلى نمط الحوار عند إغلاق الرسائل.
- جميع الملفات في إصدار واحد قبل نشره، مع أسماء توضيحية وبصمات SHA-256. Google TV ليس ملف LG ولا ملف الجوال.
- Samsung يستخدم TVWindow لعرض HDMI خلف الواجهة؛ الحزمة مستوحاة من تطبيق Sefflex/CouchSubs وفق AGPL-3.0. أداة توقيع/تثبيت وتعليمات عربية مرفقة، وتتطلب شهادة المستخدم الخاصة.

## حدود التحقق والتحديث

فحوص محاكاة وواجهة، واختبارات اختيار المناطق وعقد Samsung API والتوقيع والبصمات، لا تعني اختبار كل Xbox/PS5 أو موديل شاشة فعلي. مسار HDMI داخل التطبيق قد يختلف في Game Mode وHDR وVRR و120Hz. نسخة Google TV 0.3.8 وLG 1.0.8 لم تتغير في هذا الإصدار؛ جُمعت ملفاتها لتسهيل التنزيل. تحديث Android يحتاج موافقة النظام، وLG يحتاج أدوات التثبيت، وSamsung المطوّر يحتاج إعادة التوقيع بنفس الشهادة. لا ترسل شهادتك أو مفتاح API إلى GitHub.
