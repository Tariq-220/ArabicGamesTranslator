"""Build the complete installer ZIP and corresponding source; never include credentials."""
from pathlib import Path
import argparse, hashlib, io, json, zipfile

def package(root,publish,runtime,output):
    info=json.loads((root/'release'/'version.json').read_text());version=info['version']
    files=[]
    for p in sorted(publish.rglob('*')):
        if p.is_file() and p.suffix!='.pdb' and 'tv-app' not in p.relative_to(publish).parts:
            files.append((p,p.relative_to(publish).as_posix()))
    for p in sorted((root/'local-engine').iterdir()):
        if p.is_file() and p.suffix in ('.py','.json','.txt','.cmd') and not p.name.startswith('test_'):
            files.append((p,'local-engine/'+p.name))
    for p in sorted((runtime/'python').rglob('*')):
        if p.is_file():files.append((p,'local-engine/python/'+p.relative_to(runtime/'python').as_posix()))
    for name in ['get-pip.py','PROVENANCE.txt','hymt-runtime-win-x64.zip']:files.append((runtime/name,'local-engine/'+name))
    assert (publish/'GameTranslator.exe').exists()
    assert len({name for _,name in files})==len(files)
    manifest={'version':version,'files':[dict(path=name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p,name in files]}
    source=io.BytesIO()
    with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED,6) as z:
        for p in sorted(root.rglob('*')):
            if not p.is_file():continue
            rel=p.relative_to(root)
            if any(s in rel.parts for s in ['.git','bin','obj','build','.gradle','node_modules','publish','out','dist','runtimes','__pycache__','test-user','test-desktop','ArabicGames-Windows']):continue
            if p.name=='local.properties' or p.suffix.lower() in ['.exe','.dll','.apk','.zip','.nupkg','.jks','.keystore','.p12','.pfx','.gguf','.pyc']:continue
            z.write(p,'ArabicGames-Source/'+rel.as_posix())
    output.mkdir(parents=True,exist_ok=True)
    archive=output/f'ArabicGames-Windows-{version}-Full-Install.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,6) as z:
        for p,name in files:z.write(p,'ArabicGames-Windows/'+name)
        for name in ['Install.ps1','Install.cmd','Rollback.cmd']:z.write(root/'installer'/name,name)
        z.writestr('manifest.json',json.dumps(manifest,ensure_ascii=False))
        z.writestr(f'ArabicGames-Source-{version}.zip',source.getvalue())
        z.write(root/'LICENSE','LICENSE');z.write(root/'NOTICE','NOTICE')
        z.writestr('START-HERE-ar.txt','فك ضغط الحزمة وشغّل Install.cmd مرة واحدة. من بعدها يفحص التطبيق إصدارات GitHub وينزّل الجديد تلقائيًا خارج جلسة اللعب. اضغط تثبيت التحديث وإعادة التشغيل عندما يناسبك. المفاتيح والإعدادات محفوظة خارج مجلدات الإصدارات، والنسخة السابقة تبقى للرجوع عبر Rollback.cmd. تحميل التحديث يتطلب مستودع GitHub عامًا ومربوطًا داخل إعداد الإصدار.\n')
    with zipfile.ZipFile(archive) as z:assert z.testzip() is None
    activation=output/'ArabicGames-Enable-AutoUpdate.zip'
    with zipfile.ZipFile(activation,'w',zipfile.ZIP_DEFLATED) as z:
        for name in ['Enable-AutoUpdate.cmd','Enable-AutoUpdate.ps1']:z.write(root/'release'/name,name)
        z.writestr('START-HERE-ar.txt','فك الضغط وشغّل Enable-AutoUpdate.cmd مرة واحدة. ينزّل أحدث نسخة متحققًا من بصمتها ويثبتها. بعد ذلك يفحص التطبيق وينزّل التحديثات خارج اللعب، وتثبتها من زر التطبيق. لا يحتاج مفتاح GitHub.\n')
    digest=hashlib.sha256(archive.read_bytes()).hexdigest()
    (output/'SHA256SUMS.txt').write_text(digest+'  '+archive.name+'\n',encoding='ascii')
    print(archive,archive.stat().st_size,digest)
    return archive
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--publish',required=True);p.add_argument('--runtime',required=True);p.add_argument('--output',required=True);a=p.parse_args()
    package(Path(__file__).resolve().parents[1],Path(a.publish),Path(a.runtime),Path(a.output))
