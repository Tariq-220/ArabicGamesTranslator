# SPDX-License-Identifier: AGPL-3.0-only
import hashlib
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import threading
import unittest
import urllib.request
import urllib.error
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import zipfile
from hymt_engine import HyMtEngine, completion_payload, extract_runtime
from server import make_handler

class HyTests(unittest.TestCase):
    def test_isolated_python_can_import_sibling_module_from_unrelated_folder(self):
        server = Path(__file__).resolve().with_name('server.py')
        code = 'import runpy; runpy.run_path(' + repr(str(server)) + '); import hymt_engine; print(hymt_engine.REVISION)'
        with tempfile.TemporaryDirectory() as folder:
            result = subprocess.run([sys.executable, '-I', '-c', code], cwd=folder, capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode,0,result.stderr)
        self.assertIn('265b2e615a7dc9b06c435dc878829ad99a512ba2',result.stdout)

    def test_prompt_uses_translation_template_and_bounds(self):
        p = completion_payload('Through the palms.')
        self.assertTrue(p['prompt'].startswith('<｜hy_begin▁of▁sentence｜><｜hy_User｜>'))
        self.assertIn('into Arabic, without additional explanation.', p['prompt'])
        self.assertTrue(p['prompt'].endswith('Through the palms.<｜hy_Assistant｜>'))
        self.assertLessEqual(completion_payload('x'*1200)['n_predict'], 256)
        self.assertEqual(p['top_k'], 20)

    def test_archive_checksum_and_path_traversal(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); archive=root/'runtime.zip'
            with zipfile.ZipFile(archive,'w') as z:
                z.writestr('../escape.exe',b'bad')
            sha=hashlib.sha256(archive.read_bytes()).hexdigest()
            with self.assertRaisesRegex(RuntimeError,'checksum'):
                extract_runtime(archive,root/'extract','bad')
            with self.assertRaisesRegex(RuntimeError,'Unsafe'):
                extract_runtime(archive,root/'extract',sha)
            self.assertFalse((root/'escape.exe').exists())

    def test_backend_translation_and_output_limit(self):
        seen=[]
        class Handler(BaseHTTPRequestHandler):
            def log_message(self,*args): pass
            def do_POST(self):
                seen.append((self.headers.get('Authorization'),json.loads(self.rfile.read(int(self.headers['Content-Length'])))))
                data=json.dumps({'content':'عبر النخيل.','stop_type':'limit' if len(seen)>1 else 'eos'}).encode()
                self.send_response(200);self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
        http=ThreadingHTTPServer(('127.0.0.1',0),Handler)
        thread=threading.Thread(target=http.serve_forever,daemon=True);thread.start()
        try:
            engine=HyMtEngine.__new__(HyMtEngine);engine.endpoint='http://127.0.0.1:'+str(http.server_port);engine.key='test-secret';engine.http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
            self.assertEqual(engine.translate('Through the palms.'),'عبر النخيل.')
            self.assertEqual(seen[0][0],'Bearer test-secret')
            self.assertNotIn('test-secret',json.dumps(seen[0][1]))
            with self.assertRaisesRegex(RuntimeError,'limit'): engine.translate('Through the palms.')
        finally: http.shutdown();http.server_close();thread.join()

    def test_bridge_auth_model_mismatch_and_translation(self):
        class Fake:
            name='hy-mt'
            def translate(self,text): return 'واصل.'
        http=ThreadingHTTPServer(('127.0.0.1',0),make_handler(Fake(),'test-token'))
        thread=threading.Thread(target=http.serve_forever,daemon=True);thread.start()
        try:
            opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
            def request(model,token='test-token'):
                payload={'text':['Go.'],'source_lang':'EN','target_lang':'AR','model':model}
                return opener.open(urllib.request.Request('http://127.0.0.1:'+str(http.server_port)+'/translate',data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'}))
            with request('hy-mt') as response: self.assertEqual(json.load(response)['translations'][0]['text'],'واصل.')
            with self.assertRaises(urllib.error.HTTPError) as error: request('quickmt')
            self.assertEqual(error.exception.code,400)
            self.assertEqual(json.load(error.exception)['error'],'model_mismatch')
            with self.assertRaises(urllib.error.HTTPError) as error: request('hy-mt','wrong')
            self.assertEqual(error.exception.code,401)
        finally: http.shutdown();http.server_close();thread.join()

if __name__=='__main__': unittest.main()
