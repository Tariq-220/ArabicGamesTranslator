"""Collect public, voluntarily submitted issue reports as pending data, never approved translations."""
import hashlib,json,os,re,urllib.request,urllib.parse
from pathlib import Path
CREDENTIAL=re.compile(r'AIza[A-Za-z0-9_-]{25,}|sk-[A-Za-z0-9_-]{20,}|(?:ghp_|github_pat_)[A-Za-z0-9_]{15,}')
def normalized(value):return ' '.join(value.replace('’',"'").lower().split())
def game_name(value):
 k=normalized(value)
 return 'Grand Theft Auto VI' if k in ('gta6','gta 6','gta vi','grand theft auto 6','grand theft auto vi') else 'Red Dead Redemption 2' if k in ('rdr2','red dead redemption 2') else value.strip()
def field(body,label):
 m=re.search(r'^### '+re.escape(label)+r'\s*\n(.*?)(?=^### |\Z)',body,re.M|re.S)
 if not m:return ''
 value=m.group(1).strip()
 return '' if value=='_No response_' else value

def parse_report(issue):
 body=issue.get('body') or ''
 if not isinstance(body,str) or len(body)>20000 or CREDENTIAL.search(body):raise ValueError('oversized_or_sensitive')
 raw=field(body,'بيانات البلاغ') or body
 raw=re.sub(r'^```(?:json)?\s*|\s*```$','',raw.strip())
 try:
  value=json.loads(raw)
  if not isinstance(value,dict):raise ValueError('invalid_report')
  report={k:value.get(k,'') for k in ('game','engine','source','arabic')}
  report['proposed']=value.get('proposed','')
 except json.JSONDecodeError:
  # Existing phone app clipboard format; context is never guessed to be a correction.
  def match(pattern):
   m=re.search(pattern,raw,re.S|re.M);return m.group(1).strip() if m else ''
  report=dict(game=match(r'^اللعبة:\s*([^\n]+)'),engine=match(r'^المحرك:\s*([^\n]+)'),source=match(r'^English:\s*\n(.*?)\nالعربية الحالية:'),arabic=match(r'^العربية الحالية:\s*\n(.*?)\nالتصحيح المقترح:'),proposed=match(r'^التصحيح المقترح:\s*(.*)'))
 proposal=field(body,'الترجمة العربية الصحيحة المقترحة')
 if proposal:report['proposed']=proposal
 elif isinstance(report.get('proposed'),str) and isinstance(report.get('arabic'),str) and normalized(report['proposed'])==normalized(report['arabic']):report['proposed']=''
 if report['proposed'] in ('غير محدد','غير محدد.','لا يوجد'):report['proposed']=''
 limits={'game':200,'engine':200,'source':2000,'arabic':3000,'proposed':3000}
 for key,limit in limits.items():
  value=report[key]
  if not isinstance(value,str) or len(value)>limit:raise ValueError('invalid_'+key)
  report[key]=value.strip()
 if not report['game'] or not report['source'] or report['game']=='*':raise ValueError('missing_game_or_source')
 if report['proposed'] and not re.search('[\u0600-\u06ff]',report['proposed']):raise ValueError('arabic_proposal_required')
 report['game']=game_name(report['game']);report['context']=field(body,'التصحيح المقترح والسياق')[:1500]
 return report

def collect(issues,approved):
 groups={};rejected=[];people={}
 lookup={(game_name(e['game']),normalized(e['source'])):e['arabic'] for e in approved.get('entries',[]) if e.get('reviewed') is True}
 for issue in issues:
  if 'pull_request' in issue:continue
  if not any((label.get('name') if isinstance(label,dict) else label)=='translation-review' for label in issue.get('labels',[])):continue
  number=issue.get('number')
  if not isinstance(number,int) or number<=0:continue
  try:r=parse_report(issue)
  except (ValueError,TypeError):rejected.append(number);continue
  key=(r['game'],normalized(r['source']),normalized(r['proposed']))
  identifier=hashlib.sha256('\n'.join(key).encode()).hexdigest()[:24]
  group=groups.setdefault(key,dict(id=identifier,game=r['game'],source=r['source'],proposed=r['proposed'],original_translations=[],engines=[],reports=[],contexts=[],reviewed=False,status='pending'))
  if number not in group['reports']:group['reports'].append(number)
  for target,value in [('original_translations',r['arabic']),('engines',r['engine']),('contexts',r['context'])]:
   if value and value not in group[target] and len(group[target])<5:group[target].append(value)
  # One account counts once. Counts never approve a translation.
  account=(issue.get('user') or {}).get('id');people.setdefault(identifier,set())
  if isinstance(account,int):people[identifier].add(account)
 variants={}
 for key,g in groups.items():
  if g['proposed']:variants.setdefault(key[:2],set()).add(key[2])
 for key,g in groups.items():
  g['reports'].sort();g['contributors']=len(people[g['id']]);g['conflicting_proposals']=len(variants.get(key[:2],set()))>1
  if not g['proposed']:g['status']='needs_correction'
  elif lookup.get(key[:2])==g['proposed']:g['status']='published'
  elif g['conflicting_proposals']:g['status']='conflict'
  if g['proposed']:g['approval_candidate']={'kind':'sentence','game':g['game'],'source':g['source'],'arabic':g['proposed'],'reason':'Reviewed source and context from reports '+', '.join('#'+str(n) for n in g['reports'][:20])}
 return {'format':'arabic-games-pending-feedback-v1','notice':'Unreviewed voluntary public reports. Counts are not proof of correctness. Never load as a translation pack.','groups':sorted(groups.values(),key=lambda g:g['id']),'rejected_reports':sorted(set(rejected))}

def main():
 root=Path(__file__).resolve().parents[1];repo=os.environ['GITHUB_REPOSITORY']
 if repo!=json.loads((root/'release/version.json').read_text())['repository']:raise ValueError('Wrong repository')
 headers={'Authorization':'Bearer '+os.environ['GH_TOKEN'],'Accept':'application/vnd.github+json','User-Agent':'ArabicGames-feedback-review'};issues=[]
 for page in range(1,11):
  url='https://api.github.com/repos/'+repo+'/issues?state=all&labels=translation-review&per_page=100&page='+str(page)
  with urllib.request.urlopen(urllib.request.Request(url,headers=headers),timeout=30) as response:raw=response.read(6_000_001)
  if len(raw)>6_000_000:raise ValueError('Response too large')
  rows=json.loads(raw);issues.extend(rows)
  if len(rows)<100:break
 result=collect(issues,json.loads((root/'community/reviewed-dialogue.json').read_text()));out=root/'community/pending-feedback.json';out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print('Pending feedback groups:',len(result['groups']),'Rejected report count:',len(result['rejected_reports']))
if __name__=='__main__':main()
