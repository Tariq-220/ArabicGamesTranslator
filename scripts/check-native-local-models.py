# SPDX-License-Identifier: AGPL-3.0-only
"""Verify pinned model packages and record real host outputs; not an Android adequacy benchmark."""
import argparse,hashlib,json,subprocess,time,urllib.request,zipfile
from pathlib import Path,PurePosixPath
p=argparse.ArgumentParser();p.add_argument('--manifest',required=True);p.add_argument('--work',required=True);p.add_argument('--smoke',required=True);p.add_argument('--output',required=True);a=p.parse_args()
root=Path(a.work);root.mkdir(parents=True,exist_ok=True)
def download(url,path,size,digest):
 if not path.is_file() or path.stat().st_size!=size or hashlib.sha256(path.read_bytes()).hexdigest()!=digest:
  with urllib.request.urlopen(url,timeout=60) as response,path.open('wb') as out:
   while b:=response.read(1024*1024):out.write(b)
 assert path.stat().st_size==size,(path.name,path.stat().st_size,size)
 assert hashlib.sha256(path.read_bytes()).hexdigest()==digest,path.name
samples=['Keep going. Through the palms.',"You'll be fine... just don't step on it.",'Come on, you big dozy lizard. Rouse yourself.','We need to get above the clouds. Use the burner.','We should still have some decent views over the river.','Now the world is your oyster.']
rows=[]
for spec in json.loads(Path(a.manifest).read_text())['models']:
 folder=root/spec['id'];folder.mkdir(exist_ok=True)
 if 'archive' in spec:
  item=spec['archive'];archive=root/(spec['id']+'.zip');download(item['url'],archive,item['bytes'],item['sha256'])
  with zipfile.ZipFile(archive) as z:
   for name in z.namelist():
    assert not PurePosixPath(name).is_absolute() and '..' not in PurePosixPath(name).parts
   for item in spec['files']:
    names=[n for n in z.namelist() if PurePosixPath(n).name==item['name']];assert len(names)==1,names
    info=z.getinfo(names[0]);assert info.file_size==item['bytes'];data=z.read(info);assert hashlib.sha256(data).hexdigest()==item['sha256'];(folder/item['name']).write_bytes(data)
 else:
  for item in spec['files']:download(item['url'],folder/item['name'],item['bytes'],item['sha256'])
 for target in (['ar','ars'] if spec['id']=='jisr-int8' else ['ar']):
  for source in samples:
   started=time.monotonic();result=subprocess.run([a.smoke,str(folder),str(spec['mode']),target,source],capture_output=True,text=True,timeout=90);elapsed=round((time.monotonic()-started)*1000)
   assert result.returncode==0,result.stderr[-1500:]
   output=result.stdout.strip();assert output and any('\u0600'<=ch<='\u06ff' for ch in output),output
   rows.append({'model':spec['id'],'target':target,'source':source,'arabic':output,'host_process_ms_including_model_load':elapsed})
Path(a.output).write_text(json.dumps({'device':'GitHub Linux host CPU, not the user phone/laptop','quality_reviewed':False,'rows':rows},ensure_ascii=False,indent=2))
print('Verified model smoke cases:',len(rows))
