"""Check public pre-signed deliveries; the private signing key never enters CI."""
from pathlib import Path
import json,hashlib,re,subprocess,shutil,os
root=Path(__file__).resolve().parents[1];deliveries=root/'signed-deliveries';manifest=json.loads((deliveries/'manifest.json').read_text());assert manifest['schema']==1
cert='8bfb9b64929ed8a0565eaf89ab61469ef91370ce968e5a99873fa437d62e02ca'
build_tools=Path(os.environ['ANDROID_HOME'])/'build-tools'/'35.0.0';signer=build_tools/'apksigner';aapt=build_tools/'aapt';assert signer.is_file() and aapt.is_file()
out=root/'out'/'signed-android';out.mkdir(parents=True,exist_ok=True);seen=set();checked=[]
for entry in manifest['files']:
 platform=entry['platform'];version=entry['version'];assert platform in ('phone','tv') and platform not in seen;seen.add(platform);assert re.fullmatch(r'\d+\.\d+\.\d+',version)
 name=f'ArabicGames-Phone-{version}-arm64.apk' if platform=='phone' else f'ArabicGames-TV-{version}.apk'
 assert entry['path']==f'{platform}/{version}/{name}' and entry['name']==name and entry['certificate_sha256']==cert
 file=deliveries/entry['path'];assert 0<file.stat().st_size==entry['bytes']<250_000_000
 assert hashlib.sha256(file.read_bytes()).hexdigest()==entry['sha256']
 result=subprocess.run([str(signer),'verify','--verbose','--print-certs',str(file)],check=True,capture_output=True,text=True).stdout
 assert f'certificate SHA-256 digest: {cert}' in result
 if platform=='phone' and tuple(map(int,version.split('.'))) >= (0,4,14):
  for scheme in ['v1 scheme (JAR signing)','v2 scheme (APK Signature Scheme v2)','v3 scheme (APK Signature Scheme v3)']:assert 'Verified using '+scheme+': true' in result,'Missing compatible signing scheme: '+scheme
 info=subprocess.run([str(aapt),'dump','badging',str(file)],check=True,capture_output=True,text=True).stdout
 package='dev.arabicgames.phone' if platform=='phone' else 'dev.arabicgames.tv'
 assert re.search(r"package: name='"+re.escape(package)+r"' versionCode='"+str(entry['versionCode'])+r"' versionName='"+re.escape(version)+r"'",info)
 assert 'application-debuggable' not in info
 shutil.copyfile(file,out/name);checked.append(entry)
assert seen=={'phone','tv'}
(out/'verified.json').write_text(json.dumps({'files':checked},indent=2))
(out/'SHA256SUMS-Android.txt').write_text(''.join(e['sha256']+'  '+e['name']+'\n' for e in checked))
print('Verified independently signed phone and TV packages. Private signing key not needed.')
