خادم تجريبي للنماذج المحلية — ليس المسار الأساسي الموصى به لجودة حوارات الألعاب

Windows 10/11 x64 مع Python 3.11 أو 3.12. فك الملفات في مجلد، وافتح Terminal فيه.
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt

QuickMT:
.venv\Scripts\python server.py --host 192.168.0.10 --model quickmt
استبدل العنوان بعنوان IPv4 الحقيقي للابتوب. أول مرة يحتاج الإنترنت لتنزيل النموذج، ثم يسخّن النموذج قبل فتح المنفذ. انتظر Ready. بعد التنزيل يمكن إضافة --offline.

Jisr (اختياري):
.venv\Scripts\python -m pip install torch==2.5.1 --index-url https://download.pytorch.org/whl/cpu
.venv\Scripts\python -m pip install -r requirements-jisr.txt
.venv\Scripts\python server.py --host 192.168.0.10 --model jisr-msa
أو --model jisr-saudi. لا تشغل أكثر من خادم على المنفذ نفسه. RAM وإصدار CPU يؤثران في السرعة؛ لا يوجد ضمان لأداء Ryzen 5 دون اختبار.

رمز الربط يولد في ملف داخل حساب Windows:
%USERPROFILE%\.arabicgames\local-token.txt
افتحه محليًا وانسخه إلى خانة المفتاح في تطبيقنا. لا ترسله في المحادثة أو تضعه برابط. عنوان الخادم في التطبيق http://عنوان-اللابتوب:8790 واسم النموذج يطابق الخادم (quickmt أو jisr-msa أو jisr-saudi)، ثم وافق واستخدم للجلسة.

الاتصال HTTP محلي فقط. لا تستخدم شبكة عامة أو غير موثوقة ولا تفتح المنفذ في الراوتر. الرمز ليس تشفيرًا للنقل. الخادم لا يسجل النصوص والمفاتيح، ولا يحمّل نموذجًا باختيار طلب وارد؛ النموذج تختاره عند التشغيل. طلب واحد في كل مرة ولا توجد قائمة طلبات متراكمة. لا يستقبل صورًا.

أوزان النماذج ليست مضمّنة:
QuickMT EN→AR: https://huggingface.co/quickmt/quickmt-en-ar — CC-BY-4.0، مثبت على revision 37f2b37486c29b457971295d7711b5ac1869775d.
Jisr: https://huggingface.co/oddadmix/Jisr-MT-50M-AllDialects — Apache-2.0، مثبت على revision 8803d9d4e3b53e96783968237aa32425eb83922e.
أوزان كل نموذج وشروطه منفصلة عن ترخيص كودنا AGPL-3.0. الاعتماديات المذكورة في requirements لها تراخيصها؛ لا نعيد توزيع ثنائياتها هنا.

نتائج الاختبار الحقيقي مرفقة في LOCAL-MODEL-RESULTS-ar.txt. كلاهما أخطأ في سياق الجمل؛ لا ندعي أنهما أفضل من Gemini. هذه محطة مقارنة مستوحاة من خيارات Hearth، وليست دمجًا لـHearth نفسه أو لخدمته loopback.
