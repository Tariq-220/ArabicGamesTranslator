#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo_dir=$(pwd)
ps5_work="$(cygpath -u "$RUNNER_TEMP")/arabic-ps5-windows"
mkdir -p "$ps5_work" out/ps5-windows-native
if [ ! -d "$ps5_work/chiaki/.git" ];then
 git init "$ps5_work/chiaki"
 git -C "$ps5_work/chiaki" remote add origin https://github.com/chiaki-ng/chiaki-ng.git
 git -C "$ps5_work/chiaki" fetch --depth 1 origin f30da0aaeb96fc0026cc03683153f682352e7d7c
 git -C "$ps5_work/chiaki" checkout --detach FETCH_HEAD
 git -C "$ps5_work/chiaki" submodule update --init --recursive --depth 1
fi
curl --fail --location --retry 2 https://ffmpeg.org/releases/ffmpeg-7.1.5.tar.xz -o "$ps5_work/ffmpeg.tar.xz"
printf '%s  %s\n' de668509caf9e35e3cd162473441fdb29538c6d96ed080292b3cf9e6fc5d558f "$ps5_work/ffmpeg.tar.xz" | sha256sum -c -
mkdir -p "$ps5_work/ffmpeg-src"
tar -xf "$ps5_work/ffmpeg.tar.xz" --strip-components=1 -C "$ps5_work/ffmpeg-src"
(
 cd "$ps5_work/ffmpeg-src"
 ./configure --prefix="$ps5_work/ffmpeg-install" --target-os=mingw32 --arch=x86_64 \
 --disable-programs --disable-doc --disable-debug --disable-autodetect --disable-everything \
 --enable-decoder=h264 --enable-parser=h264 --enable-avcodec --enable-avutil --enable-swscale \
 --disable-avdevice --disable-avformat --disable-avfilter --disable-postproc --disable-network --enable-static --disable-shared
 make -j 2
 make install
)
cmake -S "$repo_dir/windows/native" -B "$ps5_work/build" -G Ninja -DCMAKE_BUILD_TYPE=Release \
 -DCHIAKI_SOURCE_DIR="$ps5_work/chiaki" -DFFMPEG_PREFIX="$ps5_work/ffmpeg-install" -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
 -DPYTHON_EXECUTABLE="$(command -v python)" -DCHIAKI_LIB_ENABLE_MBEDTLS=OFF
cmake --build "$ps5_work/build" -j 2
"$ps5_work/build/gametranslator_native_test.exe"
cp "$ps5_work/build/gametranslator_native.dll" out/ps5-windows-native/
objdump -p out/ps5-windows-native/gametranslator_native.dll > out/ps5-windows-native/pe-dependencies.txt
python - <<'PY'
from pathlib import Path
import re,json,hashlib,os
p=Path('out/ps5-windows-native/gametranslator_native.dll')
libs=re.findall(r'DLL Name: (.+)',Path('out/ps5-windows-native/pe-dependencies.txt').read_text())
allowed={'kernel32.dll','ws2_32.dll','msvcrt.dll','bcrypt.dll','crypt32.dll','advapi32.dll','user32.dll','shell32.dll','ole32.dll','gdi32.dll','normaliz.dll','secur32.dll','iphlpapi.dll','ntdll.dll','ucrtbase.dll'}
assert all(x.lower().strip() in allowed or x.lower().startswith(('api-ms-win-','ext-ms-win-')) for x in libs),libs
Path('out/ps5-windows-native/native-build-info.json').write_text(json.dumps({'source':os.environ.get('GITHUB_SHA'),'chiaki':'f30da0aaeb96fc0026cc03683153f682352e7d7c','ffmpeg':'7.1.5','sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'dependencies':libs},indent=2))
PY
cp LICENSE out/ps5-windows-native/AGPL-3.0.txt
cp "$ps5_work/ffmpeg-src/COPYING.LGPLv2.1" out/ps5-windows-native/FFmpeg-LICENSE.txt
pacman -Q > out/ps5-windows-native/build-packages.txt
python - <<'PY'
from pathlib import Path
import zipfile
with zipfile.ZipFile('out/ps5-windows-native.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(Path('out/ps5-windows-native').iterdir()):z.write(p,p.name)
PY
