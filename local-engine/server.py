# SPDX-License-Identifier: AGPL-3.0-only
"""Optional CPU-only EN->AR engine bridge. Models download before listening."""
import argparse
import hmac
import ipaddress
import json
import os
from pathlib import Path
import secrets
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

# Embeddable Windows Python excludes the script folder from sys.path.
# Add only this trusted application folder, never the current working directory.
sys.path.insert(0, str(Path(__file__).resolve().parent))

QUICKMT_REVISION = '37f2b37486c29b457971295d7711b5ac1869775d'
JISR_REVISION = '8803d9d4e3b53e96783968237aa32425eb83922e'

class Engine:
    def __init__(self, name, offline=False):
        self.name = name
        from huggingface_hub import snapshot_download
        if name == 'hy-mt':
            from hymt_engine import HyMtEngine
            self.hy = HyMtEngine(offline)
        elif name == 'quickmt':
            import ctranslate2
            import sentencepiece as sp
            options = dict(revision=QUICKMT_REVISION, allow_patterns=['config.json', 'model.bin', '*vocabulary.json', '*.spm.model', 'README.md'])
            try:
                folder = Path(snapshot_download('quickmt/quickmt-en-ar', local_files_only=True, **options))
                if not all((folder / name).is_file() for name in ['model.bin', 'src.spm.model', 'tgt.spm.model']):
                    raise FileNotFoundError('Incomplete cached QuickMT snapshot')
            except (FileNotFoundError, OSError):
                if offline:
                    raise
                folder = Path(snapshot_download('quickmt/quickmt-en-ar', local_files_only=False, **options))
            self.source = sp.SentencePieceProcessor(model_proto=(folder / 'src.spm.model').read_bytes())
            self.target = sp.SentencePieceProcessor(model_proto=(folder / 'tgt.spm.model').read_bytes())
            # Native narrow Windows paths can fail under an Arabic user profile. Feed immutable
            # model bytes to CT2 instead; SentencePiece also receives bytes above.
            model_files = {p.name: p.read_bytes() for p in folder.iterdir()
                           if p.name == 'model.bin' or p.name == 'config.json' or p.name.endswith('vocabulary.json')}
            self.runtime = ctranslate2.Translator('quickmt', files=model_files, device='cpu', compute_type='int8', inter_threads=1, intra_threads=2)
        else:
            import torch
            from transformers import PreTrainedTokenizerFast, AutoModelForSeq2SeqLM
            torch.set_num_threads(2)
            folder = snapshot_download('oddadmix/Jisr-MT-50M-AllDialects', revision=JISR_REVISION,
                allow_patterns=['*.json', '*.safetensors', 'README.md'], local_files_only=offline)
            self.tokenizer = PreTrainedTokenizerFast(tokenizer_file=str(Path(folder) / 'tokenizer.json'), eos_token='</s>', pad_token='<pad>', unk_token='<unk>')
            self.runtime = AutoModelForSeq2SeqLM.from_pretrained(folder, trust_remote_code=False).eval().to('cpu')
        self.translate('Hello.')  # Warm up before the phone can submit any work.

    def translate(self, text):
        if self.name == 'hy-mt':
            return self.hy.translate(text)
        if self.name == 'quickmt':
            from blingfire import text_to_sentences
            paragraphs = []
            for paragraph in text.splitlines():
                rows = [row for row in text_to_sentences(paragraph).splitlines() if row.strip()]
                if not rows:
                    continue
                results = self.runtime.translate_batch([self.source.encode(row, out_type=str) for row in rows], beam_size=2,
                    max_decoding_length=256, repetition_penalty=1.1)
                paragraphs.append(' '.join(self.target.decode(row.hypotheses[0]) for row in results))
            return '\n'.join(paragraphs)
        import torch
        tag = '>>ars<<' if self.name == 'jisr-saudi' else '>>ara<<'
        tokens = self.tokenizer(tag + ' ' + text, return_tensors='pt', truncation=True, max_length=512, return_token_type_ids=False)
        with torch.inference_mode():
            result = self.runtime.generate(**tokens, num_beams=2, max_new_tokens=256, repetition_penalty=1.1)
        return self.tokenizer.decode(result[0], skip_special_tokens=True)


def make_handler(engine, token):
    gate = threading.Lock()
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass  # Never log request bodies, tokens or game dialogue.
        def reply(self, status, payload):
            data = json.dumps(payload, ensure_ascii=False).encode('utf-8')
            self.send_response(status)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.send_header('Cache-Control', 'no-store')
            self.end_headers()
            self.wfile.write(data)
        def do_POST(self):
            if self.path != '/translate' or self.headers.get('Origin'):
                self.reply(403, {'error': 'route_or_origin'}); return
            expected_host = self.server.server_address[0] + ':' + str(self.server.server_address[1])
            if self.headers.get('Host') != expected_host:
                self.reply(403, {'error': 'host'}); return
            auth = self.headers.get('Authorization', '')
            if not hmac.compare_digest(auth.encode(), ('Bearer ' + token).encode()):
                self.reply(401, {'error': 'pairing'}); return
            try:
                length = int(self.headers.get('Content-Length', '-1'))
                if not 0 < length <= 16000 or self.headers.get_content_type() != 'application/json':
                    raise ValueError()
                self.connection.settimeout(5)
                payload = json.loads(self.rfile.read(length))
                if set(payload) != {'text', 'source_lang', 'target_lang', 'model'}:
                    raise ValueError()
                if payload['model'] != engine.name:
                    self.reply(400, {'error': 'model_mismatch', 'expected_model': engine.name}); return
                if payload['source_lang'] != 'EN' or payload['target_lang'] != 'AR':
                    raise ValueError()
                rows = payload['text']
                if not isinstance(rows, list) or not 1 <= len(rows) <= 4 or any(not isinstance(row, str) or not 1 <= len(row) <= 1200 for row in rows):
                    raise ValueError()
            except (ValueError, TypeError, KeyError, TimeoutError):
                self.reply(400, {'error': 'request'}); return
            if not gate.acquire(timeout=5):
                self.reply(503, {'error': 'busy'}); return
            try:
                translated = [{'text': engine.translate(row)} for row in rows]
                self.reply(200, {'translations': translated})
            except Exception:
                self.reply(500, {'error': 'inference'})
            finally:
                gate.release()
    return Handler


def token_from_file(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, 'w') as stream:
            stream.write(secrets.token_urlsafe(32))
    token = path.read_text().strip()
    if not 20 <= len(token) <= 200 or any(c.isspace() for c in token):
        raise ValueError('Invalid token file; use a new local token file.')
    return token


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', choices=['quickmt', 'jisr-msa', 'jisr-saudi', 'hy-mt'], default='quickmt')
    parser.add_argument('--host', default='127.0.0.1')
    parser.add_argument('--port', type=int, default=8790)
    parser.add_argument('--offline', action='store_true')
    parser.add_argument('--token-file', type=Path, default=Path.home() / '.arabicgames' / 'local-token.txt')
    args = parser.parse_args()
    host = ipaddress.IPv4Address(args.host)
    networks = [ipaddress.IPv4Network(s) for s in ('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16', '127.0.0.0/8')]
    if not any(host in network for network in networks) or not 1024 <= args.port <= 65535:
        parser.error('Bind only to your private LAN IPv4 or loopback, on port 1024..65535.')
    token = token_from_file(args.token_file)
    print('Loading selected model on CPU; first run downloads model weights. No phone requests accepted yet.', flush=True)
    engine = Engine(args.model, args.offline)
    server = ThreadingHTTPServer((args.host, args.port), make_handler(engine, token))
    print('Ready:', args.host, args.port, 'model:', args.model, flush=True)
    print('Open your local token file to pair the phone. Do not share the token or expose this port to the Internet.', flush=True)
    try:
        server.serve_forever()
    finally:
        server.server_close()
        if hasattr(engine, 'hy'):
            engine.hy.close()

if __name__ == '__main__':
    main()
