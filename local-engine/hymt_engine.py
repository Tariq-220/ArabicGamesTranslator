# SPDX-License-Identifier: AGPL-3.0-only
"""Pinned CPU llama.cpp runtime for the official HY-MT 1.8B Q4_K_M model."""
import atexit
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
import urllib.request
import zipfile

REVISION = '265b2e615a7dc9b06c435dc878829ad99a512ba2'
FILENAME = 'HY-MT1.5-1.8B-Q4_K_M.gguf'


def completion_payload(text):
    prompt = ('<｜hy_begin▁of▁sentence｜><｜hy_User｜>'
              'Translate the following segment into Arabic, without additional explanation.\n\n'
              + text + '<｜hy_Assistant｜>')
    return dict(prompt=prompt, n_predict=min(256, max(64, len(text)//2+40)),
                temperature=0.7, top_k=20, top_p=0.6, repeat_penalty=1.05,
                seed=42, cache_prompt=True, stream=False)


def extract_runtime(archive, destination, expected_sha):
    digest = hashlib.sha256()
    with archive.open('rb') as f:
        for chunk in iter(lambda: f.read(1024*1024), b''):
            digest.update(chunk)
    if digest.hexdigest() != expected_sha:
        raise RuntimeError('CPU runtime checksum failed. Restore the runtime ZIP from the update.')
    with zipfile.ZipFile(archive) as z:
        for entry in z.infolist():
            target = (destination / entry.filename).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise RuntimeError('Unsafe runtime archive path.')
        z.extractall(destination)
    server = list(destination.rglob('llama-server.exe'))
    if len(server) != 1:
        raise RuntimeError('CPU runtime missing llama-server.exe.')
    return server[0]


class HyMtEngine:
    def __init__(self, offline=False):
        if os.name != 'nt':
            raise RuntimeError('This bundled CPU runtime targets Windows x64 only.')
        from huggingface_hub import hf_hub_download
        root = Path(__file__).resolve().parent
        manifest = json.loads((root/'hymt-runtime.json').read_text())
        destination = root/'runtimes'/manifest['release']
        binary = extract_runtime(root/'hymt-runtime-win-x64.zip', destination, manifest['sha256'])
        print('HY-MT Q4_K_M: first download is about 1.13 GB. Cached after download.', flush=True)
        model = hf_hub_download('tencent/HY-MT1.5-1.8B-GGUF', FILENAME,
                               revision=REVISION, local_files_only=offline)
        with socket.socket() as s:
            s.bind(('127.0.0.1', 0))
            port = s.getsockname()[1]
        self.endpoint = 'http://127.0.0.1:' + str(port)
        self.key = secrets.token_urlsafe(32)
        self.http = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        self.process = subprocess.Popen([str(binary), '--model', model, '--host', '127.0.0.1',
            '--port', str(port), '--ctx-size', '2048', '--threads', str(min(4, os.cpu_count() or 2)),
            '--n-gpu-layers', '0', '--api-key', self.key, '--no-webui', '--log-disable'])
        atexit.register(self.close)
        deadline = time.monotonic()+120
        while time.monotonic() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError('CPU runtime exited. Check Windows x64 / Visual C++ runtime support.')
            try:
                with self.http.open(urllib.request.Request(self.endpoint+'/health',
                        headers={'Authorization':'Bearer '+self.key}), timeout=1) as response:
                    if response.status == 200:
                        return
            except (OSError, TimeoutError):
                time.sleep(0.25)
        self.close()
        raise RuntimeError('CPU model initialization timed out (120 seconds).')

    def translate(self, text):
        request = urllib.request.Request(self.endpoint+'/completion',
            data=json.dumps(completion_payload(text), ensure_ascii=False).encode('utf-8'),
            headers={'Content-Type':'application/json', 'Authorization':'Bearer '+self.key})
        with self.http.open(request, timeout=30) as response:
            result = json.load(response)
        if result.get('stop_type') == 'limit' or result.get('stopped_limit') or result.get('truncated'):
            raise RuntimeError('Model output exceeded the short-dialogue limit.')
        value = result.get('content', '').strip()
        if not value:
            raise RuntimeError('Empty HY-MT result.')
        return value

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait()
