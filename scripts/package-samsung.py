"""Build a transparent unsigned Tizen developer kit. Never fabricates a Samsung signing identity."""
import io,zipfile,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1];shared=root/'tv/webos';tizen=root/'tv/tizen';out=root/'tv/packages';out.mkdir(parents=True,exist_ok=True)
config=ET.parse(tizen/'config.xml').getroot();version=config.attrib['version'];ns={'t':'http://tizen.org/ns/widgets'};assert config.find('t:application',ns).attrib['id']=='ArGamesTV1.ArabicGames'
files={p.name:p.read_bytes() for p in shared.iterdir() if p.is_file() and p.name not in ('platform.js','appinfo.json')}
files['platform.js']=(tizen/'platform.js').read_bytes();files['config.xml']=(tizen/'config.xml').read_bytes();files['LICENSE']=(root/'LICENSE').read_bytes();files['NOTICE']=(root/'NOTICE').read_bytes()
files['index.html']=files['index.html'].replace(b'<script src="platform.js">',b'<script src="$WEBAPIS/webapis/webapis.js"></script>\n  <script src="platform.js">')
files['app.js']=files['app.js'].replace(b"var APP_VERSION = '1.0.8';",("var APP_VERSION = '"+version+"';").encode())
def archive(entries):
 b=io.BytesIO()
 with zipfile.ZipFile(b,'w',zipfile.ZIP_DEFLATED) as z:
  for name,data in sorted(entries.items()):z.writestr(name,data)
 return b.getvalue()
wgt=archive(files);assert 'author-signature.xml' not in zipfile.ZipFile(io.BytesIO(wgt)).namelist()
(out/f'ArabicGames-Samsung-{version}-unsigned.wgt').write_bytes(wgt)
kit={'project/'+name:data for name,data in files.items()};kit['Sign-and-Install.ps1']=(tizen/'Sign-and-Install.ps1').read_bytes();kit['START-HERE-ar.md']=(tizen/'START-HERE-ar.md').read_bytes()
(out/f'ArabicGames-Samsung-{version}-Developer-Kit.zip').write_bytes(archive(kit));print('Prepared unsigned Samsung developer kit',version,'— certificate and hardware testing required')
