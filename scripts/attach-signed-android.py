"""Publish signed public binaries through CI's scoped release token, never a signing secret."""
from pathlib import Path
import json,subprocess,os
root=Path(__file__).resolve().parents[1];info=json.loads((root/'release/version.json').read_text());repo=info['repository'];out=root/'out/signed-android';manifest=json.loads((out/'verified.json').read_text());standalone=os.environ.get('ANDROID_ONLY')=='true';tag='android-v'+manifest['files'][0]['version']+'-tv'+manifest['files'][1]['version'] if standalone else 'preview-'+info['version']
def gh(*args):return subprocess.check_output(['gh',*args],text=True).strip()
try:release=json.loads(gh('release','view',tag,'--repo',repo,'--json','isDraft,assets'))
except subprocess.CalledProcessError:
 if standalone:
  subprocess.run(['gh','release','create',tag,'--repo',repo,'--draft','--target',os.environ['GITHUB_SHA'],'--title','Android HDMI and local translation repair','--notes-file',str(root/'release/ANDROID-FIX-NOTES.md')],check=True)
  release=json.loads(gh('release','view',tag,'--repo',repo,'--json','isDraft,assets'))
 elif os.environ.get('GITHUB_EVENT_NAME')=='workflow_run':print('No preview draft to attach; stable release unchanged.');raise SystemExit(0)
 else:raise
if not release['isDraft']:
 assets={a['name']:a for a in release['assets']}
 assert all(e['name'] in assets for e in manifest['files']),'Published releases are immutable: prepare a new version'
 print('Preview already published; no files overwritten.');raise SystemExit(0)
subprocess.run(['gh','release','upload',tag,'--repo',repo,'--clobber',*[str(out/e['name']) for e in manifest['files']],str(out/'SHA256SUMS-Android.txt'),str(root/'community/packs/ArabicGames-GTA6-Reviewed-Pack.json')],check=True)
subprocess.run(['gh','release','edit',tag,'--repo',repo,'--target',os.environ['GITHUB_SHA'],'--notes-file',str(root/('release/ANDROID-FIX-NOTES.md' if standalone else 'release/NOTES.md')),'--title',f"ArabicGames — Phone {manifest['files'][0]['version']} · TV {manifest['files'][1]['version']}" if standalone else f"ArabicGames — Windows {info['version']} · Phone {manifest['files'][0]['version']} · TV {manifest['files'][1]['version']} (تجربة)"],check=True)
release=json.loads(gh('release','view',tag,'--repo',repo,'--json','assets'));assets={a['name']:a for a in release['assets']}
for e in manifest['files']:
 assert assets[e['name']]['size']==e['bytes']
# GitHub's API supplies the canonical asset digest; validate before publication.
all_releases=json.loads(gh('api',f'repos/{repo}/releases?per_page=20'))
current=next(r for r in all_releases if r['tag_name']==tag)
for e in manifest['files']:
 asset=next(a for a in current['assets'] if a['name']==e['name']);assert asset['digest']=='sha256:'+e['sha256']
if os.environ.get('PUBLISH_PREVIEW')=='true':
 if not standalone:
  for name in [f"ArabicGames-Windows-{info['version']}-Full-Install.zip",f"ArabicGames-Windows-{info['version']}-Update.zip"]:assert name in assets,'Desktop preparation has not finished'
 subprocess.run(['gh','release','edit',tag,'--repo',repo,'--draft=false','--prerelease=false' if standalone else '--prerelease','--latest=false'],check=True)
 print('Published verified preview with separate platform files.')
else:print('Separate Android downloads attached to the prepared draft.')
