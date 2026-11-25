"""Save generated demo-only UI images in an isolated review branch. Never save account/browser data."""
from pathlib import Path
import base64,json,os,subprocess
repo=os.environ['GITHUB_REPOSITORY'];source=os.environ['GITHUB_SHA']
assert os.environ['GITHUB_REF']=='refs/heads/rebuild/v024-vi'
def api(method,path,data=None):
    args=['gh','api','-X',method,'repos/'+repo+'/'+path]
    if data is not None:args+=['--input','-']
    p=subprocess.run(args,input=json.dumps(data) if data is not None else None,text=True,capture_output=True)
    if p.returncode:raise RuntimeError('Preview API request failed: '+path)
    return json.loads(p.stdout)
root=Path(os.environ['RUNNER_TEMP'])/'rebuild-screens'
files=list(root.glob('arabic-*.png'));assert len(files)==10
files+=list(root.glob("presentation-*.png"))
entries=[]
for path in files:
    assert path.stat().st_size<2_000_000
    blob=api('POST','git/blobs',{'content':base64.b64encode(path.read_bytes()).decode(),'encoding':'base64'})
    entries.append({'path':'screens/'+path.name,'mode':'100644','type':'blob','sha':blob['sha']})
entries.append({'path':'screens/source.txt','mode':'100644','type':'blob','content':source+'\nDemo screens only, no real account data.\n'})
tree=api('POST','git/trees',{'tree':entries})
commit=api('POST','git/commits',{'message':'Generated rebuild preview '+source[:12],'tree':tree['sha'],'parents':[source],'author':{'name':'ArabicGames CI','email':'ci@users.noreply.github.com'}})
ref='heads/rebuild-preview-images'
try:old=api('GET','git/ref/'+ref)
except RuntimeError:api('POST','git/refs',{'ref':'refs/'+ref,'sha':commit['sha']})
else:
    previous=api('GET','git/commits/'+old['object']['sha'])
    assert previous['message'].startswith('Generated rebuild preview ') and previous['author']['name']=='ArabicGames CI'
    api('PATCH','git/refs/'+ref,{'sha':commit['sha'],'force':True})
print('Saved 10 demo UI screenshots to rebuild-preview-images at '+commit['sha'])
