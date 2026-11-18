"""Maintainer-only approval: validate an explicit correction and update the shared exact-match pack."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--game',required=True);p.add_argument('--source',required=True);p.add_argument('--arabic',required=True);p.add_argument('--reason',required=True);a=p.parse_args()
for value,limit in [(a.game,200),(a.source,2000),(a.arabic,3000),(a.reason,2000)]:
 if not value.strip() or len(value)>limit:raise ValueError('Empty or oversized correction')
root=Path(__file__).resolve().parents[1];path=root/'community/reviewed-dialogue.json';data=json.loads(path.read_text());key=lambda s:' '.join(s.lower().split())
entries=[e for e in data['entries'] if (key(e['game']),key(e['source']))!=(key(a.game),key(a.source))]
entries.append(dict(game=a.game.strip(),source=a.source.strip(),arabic=a.arabic.strip(),reviewed=True,reason=a.reason.strip()));data['entries']=entries;data['revision']+=1
text=json.dumps(data,ensure_ascii=False,indent=2)+'\n';path.write_text(text);(root/'android/phone/src/main/assets/reviewed-dialogue.json').write_text(text)
print('Approved exact-match corrections:',len(entries),'revision:',data['revision'])
