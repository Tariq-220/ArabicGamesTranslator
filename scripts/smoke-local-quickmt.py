"""One real CPU-model smoke test before final packaging; no game capture/account/API key needed."""
import json,os,re,sys,tempfile,time
from pathlib import Path
root=Path(__file__).resolve().parents[1]
os.environ.setdefault('HF_HOME',str(Path(tempfile.gettempdir())/'ArabicGames-اختبار-النموذج'))
sys.path.insert(0,str(root/'local-engine'))
from server import Engine
engine=Engine('quickmt')
examples=["Open the door.","Thank you.","Are you sure you've got all the weapons?", "We need to get out of here.","Jason: Let's go.","Lucia: Are you ready?", "I don't know what you're talking about.","Get in the car.","I'll see you tomorrow.","Wait for me.","Follow Lucia.","Map Pin Unavailable", "Dropped off scuba to yuh boat man. Happy hunting!", "With Lucia out of prison, it's time to catch up."]
results=[]
for source in examples:
    start=time.perf_counter();arabic=engine.translate(source)
    assert re.search('[\u0620-\u064a]',arabic),(source,arabic)
    results.append(dict(source=source,translation=arabic,model_ms=round((time.perf_counter()-start)*1000)))
assert 'باب' in results[0]['translation'],results[0]
output=Path(os.environ.get('RUNNER_TEMP',tempfile.gettempdir()))/'quickmt-smoke.json'
output.write_text(json.dumps({'model':'quickmt-en-ar','model_only':True,'results':results},ensure_ascii=False,indent=2),encoding='utf-8')
# Second start must use already-cached weights without a network requirement.
Engine('quickmt',offline=True)
print('Real QuickMT CPU smoke passed on 14 inputs; cached offline restart passed. Model timings exclude OCR/network/display.')
