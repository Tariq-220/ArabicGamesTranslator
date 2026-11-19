"""Small, hash-checked bridge-only repair for installed Windows releases (including 0.4.24)."""
from pathlib import Path
import hashlib,zipfile
root=Path(__file__).resolve().parents[1]
out=root/'out/quickmt-repair';out.mkdir(parents=True,exist_ok=True)
payload=root/'local-engine/server.py'
with zipfile.ZipFile(out/'QuickMT-Path-Fix.zip','w',zipfile.ZIP_DEFLATED) as z:
 z.write(payload,'server.py')
 z.writestr('server.sha256',hashlib.sha256(payload.read_bytes()).hexdigest()+'\n')
 for name in ['Fix-QuickMT.cmd','Fix-QuickMT.ps1']:z.write(root/'installer'/name,name)
 for name in ['LICENSE','NOTICE']:z.write(root/name,name)
 z.writestr('اقرأني.txt','أغلق نافذة QuickMT السوداء، فك الضغط كاملًا وشغّل Fix-QuickMT.cmd. يكتشف النسخ المثبتة ويصلح server.py فقط، مع نسخة احتياطية. لا يغيّر اسم مستخدم Windows ولا يحذف ملفات النموذج أو المفاتيح. إذا طُلب مجلد فاختر الذي يحتوي GameTranslator.exe، ثم أعد تشغيل النموذج من التطبيق.\n'.encode('utf-8-sig'))
print(out/'QuickMT-Path-Fix.zip')
