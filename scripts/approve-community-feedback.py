"""Maintainer-reviewed feedback only. No issue body is executed or automatically trusted."""
import copy,json,os,re
from pathlib import Path

def apply_review(document,feedback,confirmed):
 if not confirmed:raise ValueError('Explicit maintainer review is required')
 if not isinstance(feedback,dict):raise ValueError('Expected a JSON object')
 data=copy.deepcopy(document);kind=feedback.get('kind')
 def text(name,limit):
  value=feedback.get(name)
  if not isinstance(value,str) or not value.strip() or len(value)>limit:raise ValueError('Invalid '+name)
  if re.search(r'AIza[A-Za-z0-9_-]{25,}|sk-[A-Za-z0-9_-]{20,}|(?:ghp_|github_pat_)[A-Za-z0-9_]{15,}',value):raise ValueError('Credential-like content is not allowed')
  return value.strip()
 if kind=='sentence':
  if set(feedback)!={'kind','game','source','arabic','reason'}:raise ValueError('Unexpected fields')
  game,source,arabic,reason=text('game',200),text('source',2000),text('arabic',3000),text('reason',2000)
  if not re.search('[\u0600-\u06ff]',arabic):raise ValueError('Arabic translation required')
  if game=='*':raise ValueError('Sentence corrections must be game scoped')
  key=lambda x:' '.join(x.lower().replace('’',"'").split())
  canonical=lambda x:'Grand Theft Auto VI' if key(x) in ('gta6','gta 6','gta vi','grand theft auto 6','grand theft auto vi') else 'Red Dead Redemption 2' if key(x) in ('rdr2','red dead redemption 2') else x
  game=canonical(game)
  data['entries']=[e for e in data['entries'] if (key(canonical(e['game'])),key(e['source']))!=(key(game),key(source))]
  data['entries'].append(dict(game=game,source=source,arabic=arabic,reviewed=True,reason=reason))
 elif kind=='phrase':
  if set(feedback)-{'source_meaning'}!={'kind','id','game','source_phrase','bad_arabic','replacement','reason'}:raise ValueError('Unexpected fields')
  entry={name:text(name,limit) for name,limit in [('id',80),('game',200),('source_phrase',160),('bad_arabic',160),('replacement',160),('reason',2000)]}
  if not re.fullmatch('[a-z0-9-]+',entry['id']) or len(entry['source_phrase'])<4 or entry['bad_arabic']==entry['replacement']:raise ValueError('Invalid phrase correction')
  if 'source_meaning' in feedback:entry['source_meaning']=text('source_meaning',200)
  rules=[e for e in data.get('rules',[]) if e['id']!=entry['id']]
  if len(rules)>=100:raise ValueError('Too many rules')
  rules.append(dict(**entry,reviewed=True));data['rules']=rules
 else:raise ValueError('Unknown correction kind')
 data['revision']+=1
 return data

def resolve_feedback(document,identifier):
 if not re.fullmatch('[0-9a-f]{24}',identifier):raise ValueError('Invalid feedback identifier')
 group=next((g for g in document.get('groups',[]) if g.get('id')==identifier),None)
 if not group or group.get('status')=='published' or not group.get('proposed') or not isinstance(group.get('approval_candidate'),dict):raise ValueError('No pending proposed correction')
 return group['approval_candidate']

def main():
 root=Path(__file__).resolve().parents[1];raw=os.environ.get('REVIEW_JSON','');identifier=os.environ.get('FEEDBACK_ID','').strip()
 if identifier:
  if raw.strip():raise ValueError('Select an indexed correction or supply JSON, not both')
  raw=json.dumps(resolve_feedback(json.loads((root/'community/pending-feedback.json').read_text()),identifier),ensure_ascii=False)
 if len(raw)>10000:raise ValueError('Feedback too large')
 path=root/'community/reviewed-dialogue.json';data=apply_review(json.loads(path.read_text()),json.loads(raw),os.environ.get('CONFIRMED_REVIEW')=='true');encoded=json.dumps(data,ensure_ascii=False,indent=2)+'\n'
 for p in [path,root/'android/phone/src/main/assets/reviewed-dialogue.json']:p.write_text(encoded)
 pack=root/'community/packs/ArabicGames-GTA6-Reviewed-Pack.json';p=json.loads(pack.read_text());p['lines']={e['source']:e['arabic'] for e in data['entries'] if e['game']=='Grand Theft Auto VI'};pack.write_text(json.dumps(p,ensure_ascii=False,indent=2)+'\n')
 print('Published reviewed data revision',data['revision'],'— no model weights modified')
if __name__=='__main__':main()
