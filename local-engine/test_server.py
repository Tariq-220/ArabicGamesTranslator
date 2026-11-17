# SPDX-License-Identifier: AGPL-3.0-only
import http.client
import json
import threading
import unittest
from http.server import ThreadingHTTPServer
from server import make_handler

class Stub:
    name = 'quickmt'
    def translate(self, text):
        return 'مرحبًا.'

class ServerTest(unittest.TestCase):
    def test_authenticated_text_only_bridge(self):
        server = ThreadingHTTPServer(('127.0.0.1', 0), make_handler(Stub(), 'local-test-token'))
        thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
        try:
            def request(token, payload):
                connection = http.client.HTTPConnection(*server.server_address, timeout=2)
                connection.request('POST', '/translate', json.dumps(payload), {'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token})
                response = connection.getresponse(); status = response.status; body = json.loads(response.read()); connection.close(); return status, body
            body = {'text': ['Hello.'], 'source_lang': 'EN', 'target_lang': 'AR', 'model': 'quickmt'}
            self.assertEqual(401, request('wrong', body)[0])
            status, result = request('local-test-token', body)
            self.assertEqual(200, status); self.assertEqual('مرحبًا.', result['translations'][0]['text'])
            body['target_lang'] = 'TR'; self.assertEqual(400, request('local-test-token', body)[0])
        finally:
            server.shutdown(); server.server_close(); thread.join()

if __name__ == '__main__':
    unittest.main()
