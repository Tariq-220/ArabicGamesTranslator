#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo_dir=$(pwd)
ct2_work="${RUNNER_TEMP:-/tmp}/arabic-ct2-native"
mkdir -p "$ct2_work" out/ct2-native
export GIT_LFS_SKIP_SMUDGE=1
if [ ! -d "$ct2_work/hearth/.git" ];then
 git init "$ct2_work/hearth"
 git -C "$ct2_work/hearth" remote add origin https://github.com/Sal7one/Hearth.git
 git -C "$ct2_work/hearth" fetch --depth 1 origin bc6823b8ec8254eefa222d28e60d70ccdd7d67ce
 git -C "$ct2_work/hearth" checkout --detach FETCH_HEAD
fi
export ANDROID_NDK_HOME="$ANDROID_HOME/ndk/27.2.12479018"
if [ ! -d "$ANDROID_NDK_HOME" ];then "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" 'ndk;27.2.12479018';fi
rustup target add aarch64-linux-android
export HEARTH_BUILD_JOBS=2
bash "$ct2_work/hearth/scripts/translation/ct2/build-runtime.sh" arm64-v8a
cp "$ct2_work/hearth/build/ct2-runtime/android-arm64-v8a/libtransiber_ct2.so" out/ct2-native/
cp "$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so" out/ct2-native/
cp "$ct2_work/hearth/LICENSE" out/ct2-native/Hearth-LICENSE.txt
cp "$ct2_work/hearth/build/ct2-runtime/android-arm64-v8a/hearth-ct2-sources.json" out/ct2-native/
"$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip" --strip-unneeded out/ct2-native/libtransiber_ct2.so
# Prove model packages and wording on the host separately from Android timing.
CXXFLAGS="-include cstdint" bash "$ct2_work/hearth/scripts/translation/ct2/build-runtime.sh" host
python "$repo_dir/scripts/check-native-local-models.py" --manifest "$repo_dir/android/phone/src/main/assets/native-local-models.json" --work "$ct2_work/models" --smoke "$ct2_work/hearth/build/ct2-runtime/host/ct2_smoke" --output out/ct2-native/model-smoke.json
python - <<'PY'
from pathlib import Path
import json,hashlib,os,zipfile
files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('out/ct2-native').iterdir() if p.suffix=='.so'}
Path('out/ct2-native/native-build-info.json').write_text(json.dumps({'source':os.environ.get('GITHUB_SHA'),'hearth':'bc6823b8ec8254eefa222d28e60d70ccdd7d67ce','ctranslate2':'8c8cab2a954810216a45d65c1b8d56839c846e5b','abi':'arm64-v8a','minSdk':28,'files':files},indent=2))
with zipfile.ZipFile('out/ct2-native.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(Path('out/ct2-native').iterdir()):z.write(p,p.name)
PY
