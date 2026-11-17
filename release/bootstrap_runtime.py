"""Download pinned official Windows CPU runtime dependencies for CI packaging."""
from pathlib import Path
import argparse, hashlib, json, urllib.request, zipfile

def download(url, destination, expected):
    print('Downloading pinned build dependency:', destination.name)
    urllib.request.urlretrieve(url,destination)
    if hashlib.sha256(destination.read_bytes()).hexdigest()!=expected:
        destination.unlink()
        raise RuntimeError('Dependency checksum failed: '+destination.name)

def main():
    p=argparse.ArgumentParser();p.add_argument('--target',required=True);args=p.parse_args()
    target=Path(args.target);target.mkdir(parents=True,exist_ok=True)
    root=Path(__file__).resolve().parents[1]
    archive=target/'python.zip'
    download('https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip',archive,'4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3')
    with zipfile.ZipFile(archive) as z:z.extractall(target/'python')
    archive.unlink()
    (target/'python'/'python312._pth').write_text('python312.zip\n.\nLib\\site-packages\nimport site\n',encoding='ascii')
    pip_bytes=(root/'release'/'vendor'/'get-pip.py').read_bytes()
    if hashlib.sha256(pip_bytes).hexdigest()!='356e0d0de21cdc348c3394084d90939b85e4246c4d14c76aee6ca4f46646ea98':raise RuntimeError('get-pip checksum failed')
    (target/'get-pip.py').write_bytes(pip_bytes)
    runtime=json.loads((root/'local-engine'/'hymt-runtime.json').read_text())
    download(runtime['url'],target/'hymt-runtime-win-x64.zip',runtime['sha256'])
    (target/'PROVENANCE.txt').write_text('Official python.org Python 3.12.10 x64 embeddable; archive SHA256 4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3\nllama.cpp provenance: local-engine/hymt-runtime.json\n',encoding='utf-8')
if __name__=='__main__':main()
