#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo_dir=$(pwd)
ps5_work="${RUNNER_TEMP:-/tmp}/arabic-ps5-native"
mkdir -p "$ps5_work" out/ps5-native
export ANDROID_NDK_ROOT="$ANDROID_HOME/ndk/27.2.12479018"
if [ ! -d "$ANDROID_NDK_ROOT" ]; then "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" 'ndk;27.2.12479018'; fi
export PATH="$ANDROID_NDK_ROOT/toolchains/llvm/prebuilt/linux-x86_64/bin:$PATH"
python -m pip install 'protobuf==6.33.0' 'grpcio-tools==1.76.0'
printf '#!/bin/sh\nexec python -m grpc_tools.protoc "$@"\n' > "$ps5_work/protoc"
chmod +x "$ps5_work/protoc"
if [ ! -d "$ps5_work/chiaki/.git" ]; then
 git init "$ps5_work/chiaki"
 git -C "$ps5_work/chiaki" remote add origin https://github.com/chiaki-ng/chiaki-ng.git
 git -C "$ps5_work/chiaki" fetch --depth 1 origin f30da0aaeb96fc0026cc03683153f682352e7d7c
 git -C "$ps5_work/chiaki" checkout --detach FETCH_HEAD
 git -C "$ps5_work/chiaki" submodule update --init --recursive --depth 1
fi
curl --fail --location --retry 2 https://github.com/openssl/openssl/releases/download/openssl-3.5.9/openssl-3.5.9.tar.gz -o "$ps5_work/openssl.tar.gz"
printf '%s  %s\n' 603f5602e2eef00d77fbd429d34dcd5822bb301757a1bc9cdb24c670f1eb859a "$ps5_work/openssl.tar.gz" | sha256sum -c -
mkdir -p "$ps5_work/openssl-src"
tar -xzf "$ps5_work/openssl.tar.gz" --strip-components=1 -C "$ps5_work/openssl-src"
(
 cd "$ps5_work/openssl-src"
 ./Configure android-arm64 no-shared no-tests -D__ANDROID_API__=26 -fPIC --prefix="$ps5_work/openssl-install" --libdir=lib
 make -j 2
 make install_sw
)
cmake -S "$repo_dir/android/phone/src/main/cpp" -B "$ps5_work/build" -G Ninja \
 -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_ROOT/build/cmake/android.toolchain.cmake" \
 -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 -DANDROID_STL=c++_static -DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON \
 -DCHIAKI_DIR="$ps5_work/chiaki" -DOPENSSL_ROOT_DIR="$ps5_work/openssl-install" \
 -DOPENSSL_INCLUDE_DIR="$ps5_work/openssl-install/include" \
 -DOPENSSL_CRYPTO_LIBRARY="$ps5_work/openssl-install/lib/libcrypto.a" \
 -DOPENSSL_SSL_LIBRARY="$ps5_work/openssl-install/lib/libssl.a" \
 -DPYTHON_EXECUTABLE="$(command -v python)" -DPROTOC="$ps5_work/protoc"
cmake --build "$ps5_work/build" --target gtnative -j 2
cp "$ps5_work/build/libgtnative.so" out/ps5-native/
llvm-strip --strip-unneeded out/ps5-native/libgtnative.so
llvm-readelf -l out/ps5-native/libgtnative.so > out/ps5-native/elf-program-headers.txt
llvm-readelf -d out/ps5-native/libgtnative.so > out/ps5-native/elf-dynamic.txt
python - <<'PY'
from pathlib import Path
import hashlib,json,os,re
p=Path('out/ps5-native/libgtnative.so')
loads=[int(l.split()[-1],16) for l in Path('out/ps5-native/elf-program-headers.txt').read_text().splitlines() if l.strip().startswith('LOAD')]
assert loads and all(x>=16384 for x in loads),loads
assert b'dev/arabicgames/phone/ps5/NativeBridge' in p.read_bytes()
Path('out/ps5-native/native-build-info.json').write_text(json.dumps({'abi':'arm64-v8a','minSdk':26,'ndk':'27.2.12479018','openssl':'3.5.9','chiaki':'f30da0aaeb96fc0026cc03683153f682352e7d7c','source':os.environ.get('GITHUB_SHA'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()},indent=2))
PY
cp LICENSE out/ps5-native/AGPL-3.0.txt
cp "$ps5_work/openssl-src/LICENSE.txt" out/ps5-native/OpenSSL-LICENSE.txt
python - <<'PY'
from pathlib import Path
import zipfile
with zipfile.ZipFile('out/ps5-native.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(Path('out/ps5-native').iterdir()):z.write(p,p.name)
PY
