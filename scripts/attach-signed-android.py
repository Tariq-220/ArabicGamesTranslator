"""Complete a draft release with verified APKs before making the combined stable version public."""
from pathlib import Path
import json,subprocess,os,re
root=Path(__file__).resolve().parents[1];info=json.loads((root/'release/version.json').read_text());repo=info['repository'];out=root/'out/signed-android';manifest=json.loads((out/'verified.json').read_text());standalone=os.environ.get('ANDROID_ONLY')=='true'
def gh(*args):return subprocess.check_output(['gh',*args],text=True).strip()
def view(tag):
 try:return json.loads(gh('release','view',tag,'--repo',repo,'--json','isDraft,assets'))
 except subprocess.CalledProcessError:return None
stable=False
if standalone:tag='android-v'+manifest['files'][0]['version']+'-tv'+manifest['files'][1]['version']
else:
 stable_tag='v'+info['version'];candidate=view(stable_tag)
 if candidate and candidate['isDraft']:tag=stable_tag;stable=True
 else:tag='preview-'+info['version']
release=view(tag)
if release is None:
 if standalone:
  subprocess.run(['gh','release','create',tag,'--repo',repo,'--draft','--target',os.environ['GITHUB_SHA'],'--title','ArabicGames Android updates','--notes-file',str(root/'release/ANDROID-FIX-NOTES.md')],check=True);release=view(tag)
 elif os.environ.get('GITHUB_EVENT_NAME')=='workflow_run':print('No prepared draft; existing published releases unchanged.');raise SystemExit(0)
 else:raise RuntimeError('Prepare the Windows draft first')
if not release['isDraft']:
 assert all(e['name'] in {a['name'] for a in release['assets']} for e in manifest['files']),'Published releases are immutable'
 print('Already published; no files overwritten.');raise SystemExit(0)
subprocess.run(['gh','release','upload',tag,'--repo',repo,'--clobber',*[str(out/e['name']) for e in manifest['files']],str(out/'SHA256SUMS-Android.txt'),str(root/'community/packs/ArabicGames-GTA6-Reviewed-Pack.json')],check=True)
title=f"ArabicGames {info['version']} — Windows · Android · Google TV · LG · Samsung" if stable else f"ArabicGames — Phone {manifest['files'][0]['version']} · TV {manifest['files'][1]['version']}"
subprocess.run(['gh','release','edit',tag,'--repo',repo,'--notes-file',str(root/('release/ANDROID-FIX-NOTES.md' if standalone else 'release/NOTES.md')),'--title',title],check=True)
all_releases=json.loads(gh('api',f'repos/{repo}/releases?per_page=20'));current=next(r for r in all_releases if r['tag_name']==tag);assets={a['name']:a for a in current['assets']}
for e in manifest['files']:
 a=assets[e['name']];assert a['size']==e['bytes'] and a['digest']=='sha256:'+e['sha256']
labels={e['name']:(('جوال Android '+e['version']+' — arm64 APK') if e['platform']=='phone' else ('شاشة Google TV / TCL '+e['version']+' — APK')) for e in manifest['files']}
if stable:
 required=[f"ArabicGames-Windows-{info['version']}-Full-Install.zip",f"ArabicGames-Windows-{info['version']}-Update.zip",'ArabicGames-Arabic-Guide.zip','ArabicGames-Samsung-0.1.0-Developer-Kit.zip']
 assert all(n in assets for n in required) and any(n.endswith('.ipk') for n in assets),'Some platform packages or guide are missing'
 labels.update({required[0]:'Windows '+info['version']+' — تثبيت كامل أول مرة',required[1]:'Windows '+info['version']+' — إصلاح وتحديث النسخة الموجودة','ArabicGames-Arabic-Guide.zip':'دليل عربي مصور — HTML دون إنترنت','ArabicGames-Samsung-0.1.0-Developer-Kit.zip':'Samsung Tizen 0.1.0 — مطوّر، يحتاج شهادة','ArabicGames-Samsung-0.1.0-unsigned.wgt':'Samsung WGT — غير موقّع، يحتاج توقيعًا قبل التثبيت'})
 for n in assets:
  if n.endswith('.ipk'):labels[n]='شاشة LG webOS 1.0.8 — IPK'
for name,label in labels.items():gh('api','--method','PATCH',f"repos/{repo}/releases/assets/{assets[name]['id']}",'-f','label='+label)
if stable:
 # Retain required installers and one consolidated checksum. Clean only this unpublished draft.
 for redundant in ['SHA256SUMS.txt','SHA256SUMS-Android.txt','ArabicGames-Enable-AutoUpdate.zip','ArabicGames-Samsung-0.1.0-unsigned.wgt']:
  if redundant in assets:
   gh('release','delete-asset',tag,redundant,'--repo',repo,'--yes');assets.pop(redundant)
 checks=[]
 for name,a in sorted(assets.items()):
  digest=a.get('digest','');assert re.fullmatch(r'sha256:[a-f0-9]{64}',digest),'Missing canonical checksum';checks.append(digest[7:]+'  '+name+'\n')
 sums=out/'SHA256SUMS-All.txt';sums.write_text(''.join(checks));subprocess.run(['gh','release','upload',tag,str(sums),'--repo',repo,'--clobber'],check=True)
if stable or os.environ.get('PUBLISH_PREVIEW')=='true':
 if not standalone:
  assert all(f"ArabicGames-Windows-{info['version']}-{part}.zip" in assets for part in ['Full-Install','Update'])
 subprocess.run(['gh','release','edit',tag,'--repo',repo,'--draft=false','--prerelease=false' if stable or standalone else '--prerelease','--latest' if stable else '--latest=false'],check=True);print('Published complete verified release:',tag)
else:print('Signed APKs attached to draft:',tag)
