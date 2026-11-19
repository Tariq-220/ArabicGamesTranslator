"""Small reading repair; keeps installed runtimes, models and private user settings."""
from pathlib import Path
import argparse,json,hashlib,shutil,subprocess
NAMES=['GameTranslator.exe','GameTranslator.dll','GameTranslator.Core.dll','GameTranslator.Shell.dll','GameTranslator.Translation.dll','GameTranslator.RemotePlay.dll','GameTranslator.TvInstall.dll','gametranslator_native.dll','Native-LICENSES.txt','GameTranslator.deps.json','GameTranslator.runtimeconfig.json']
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--publish',required=True);p.add_argument('--output',required=True);a=p.parse_args()
 root=Path(__file__).resolve().parents[1];out=Path(a.output);(out/'files').mkdir(parents=True,exist_ok=True)
 version=json.loads((root/'release/version.json').read_text())['version'];files=[]
 for name in NAMES:
  src=Path(a.publish)/name;assert src.is_file(),name;shutil.copyfile(src,out/'files'/name);files.append({'path':name,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()})
 name='local-engine/server.py';src=root/name;(out/'files/local-engine').mkdir(parents=True,exist_ok=True);shutil.copyfile(src,out/'files'/name);files.append({'path':name,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()})
 (out/'patch.json').write_text(json.dumps({'version':version,'files':files},indent=2),encoding='utf-8')
 for name in ['Reading-Fix.ps1','Reading-Fix.cmd']:shutil.copyfile(root/'installer'/name,out/name)
 for name in ['LICENSE','NOTICE']:shutil.copyfile(root/name,out/name)
 sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
 (out/'START-HERE-ar.txt').write_text('فك الضغط كاملًا ثم شغّل Reading-Fix.cmd. يكتشف النسخة الحالية ويثبت الإصلاح في مجلد جديد محليًا دون إعادة تنزيل النماذج أو حذف المفاتيح. إن طلب مجلدًا اختر الذي يحتوي GameTranslator.exe. بعد التشغيل اضغط بدء الترجمة؛ العربية على الكمبيوتر مطفأة حتى لا تحجب القراءة أثناء اللعب على التلفزيون. النسخة السابقة تبقى في مكانها.\nSource: https://github.com/Tariq-220/ArabicGamesTranslator/archive/'+sha+'.zip\n',encoding='utf-8-sig')
 print('Reading repair bytes:',sum(f.stat().st_size for f in out.rglob('*') if f.is_file()))
