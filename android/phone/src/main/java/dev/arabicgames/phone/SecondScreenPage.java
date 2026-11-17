// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
final class SecondScreenPage {
 static final String HTML="""
 <!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
 <title>شاشة الترجمة الثانية</title><style>body{background:#0b111c;color:white;font:20px sans-serif;text-align:center;margin:20px}#caption{font-size:36px;min-height:35vh;display:grid;place-items:center;white-space:pre-wrap}button,input{font:inherit;margin:12px;padding:10px}small{color:#acb9ca}</style>
 <h2>شاشة الترجمة الثانية</h2><small id="state">جارٍ الاتصال بالمصدر…</small><div id="caption" role="status" aria-live="polite"></div>
 <label>حجم الخط <input id="size" type="range" min="18" max="64" value="36"></label><button id="full">ملء الشاشة</button>
 <p><small>هذا الجهاز يعرض النص فقط. يجب أن يبقى جهاز القراءة والترجمة يعمل. لا تُرسل صور اللعبة لهذا المتصفح.</small></p>
 <script>
 const params=new URLSearchParams(location.search),code=params.get('code'),caption=document.querySelector('#caption'),state=document.querySelector('#state');
 let id;try{id=localStorage.receiverId||(localStorage.receiverId=crypto.randomUUID());}catch(e){id='browser-'+Math.random().toString(36).slice(2);}let revision=-1,until=0;
 document.querySelector('#size').oninput=e=>caption.style.fontSize=e.target.value+'px';document.querySelector('#full').onclick=()=>document.documentElement.requestFullscreen?.();
 async function poll(){const controller=new AbortController(),timeout=setTimeout(()=>controller.abort(),2200);try{
 const response=await fetch('/latest?code='+encodeURIComponent(code)+'&receiver='+encodeURIComponent(id),{cache:'no-store',signal:controller.signal});
 if(!response.ok)throw Error('connection');const line=await response.json();if(line.revision>=revision){revision=line.revision;caption.textContent=line.active?line.text:'';until=Date.now()+Number(line.duration||0);}state.textContent='متصل · النص من المصدر';
 }catch(e){state.textContent='انقطع الاتصال؛ تحقق أن جهاز المصدر يعمل وعلى نفس الشبكة';}finally{clearTimeout(timeout);setTimeout(poll,400);}}
 setInterval(()=>{if(Date.now()>until)caption.textContent='';},200);poll();
 </script></html>
 """;
}
