#!/usr/bin/env bash
#
# 릴리즈에 올릴 exe 를 만든다.
#
#   scripts/publish-release.sh
#
# 이 스크립트를 거치지 않고 손으로 publish 하지 않는다. 자동 업데이트가 여기에 매여 있다.
#
#   - 파일 이름: 돌고 있는 예전 버전들이 이 이름을 찾는다. 바꾸면 그들은 새 버전을 못 받는다.
#     (UpdatePlan.AssetName 과 같아야 한다)
#   - IncludeNativeLibrariesForSelfExtract: 빠뜨리면 WPF 네이티브 dll 이 exe 밖에 남아
#     받은 사람이 창조차 띄우지 못한다. 아래 검사가 잡는다.
#
# 만든 뒤에는 서명한다. 서명이 없으면 그 버전을 받은 사람들은 그다음 업데이트를 할 수 없다.
# 새 파일이 우리 것인지 대조할 기준이 사라지기 때문이다.
#
#   scripts/sign-windows.sh publish/release/MacroTyper-win-x64-standalone.exe
#
# .NET 런타임을 품지 않는 작은 exe 는 더 내지 않는다. 둘을 함께 내면 받는 사람이
# 자기 PC 에 무엇이 깔려 있는지부터 알아야 하고, 우리는 업데이트할 때 상대가 어느 쪽을
# 쓰는지 기억하고 있어야 한다. 69MB 한 번이 그 값보다 싸다.

set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT="src/MacroTyper/MacroTyper.csproj"
OUT="publish/release"
DIR="publish/win-x64-standalone"

# UpdatePlan.AssetName 과 반드시 같아야 한다.
NAME="MacroTyper-win-x64-standalone.exe"

version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -1)

if [ -z "$version" ]; then
    echo "$PROJECT 에서 <Version> 을 찾지 못했다" >&2
    exit 1
fi

echo "버전 $version"

# 태그를 이미 붙였다면 버전과 맞는지 본다.
# 어긋나면 새 버전이 나와도 아무도 알아채지 못하거나, 최신인데도 계속 업데이트를 권하게 된다.
if git rev-parse "v$version" >/dev/null 2>&1; then
    echo "  태그 v$version 있음"
else
    echo "  태그 v$version 없음 — 릴리즈 전에 붙일 것"
fi

mkdir -p "$OUT"
rm -rf "$DIR"

# PublishSingleFile 은 관리 어셈블리만 번들에 넣는다. WPF 네이티브 라이브러리
# (wpfgfx_cor3.dll, PresentationNative_cor3.dll, D3DCompiler_47_cor3.dll 등)는
# IncludeNativeLibrariesForSelfExtract 가 꺼져 있으면 exe 옆에 따로 남고,
# 그것들 없이는 창이 뜨지 않는다.
#
# 압축을 켜면 147MB 가 69MB 로 줄어든다. 시작할 때 푸느라 조금 더 걸리지만,
# 업데이트마다 그 차이를 다시 받는 편이 더 비싸다.
dotnet publish "$PROJECT" \
    -c Release -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$DIR"

# exe 말고 실행에 필요한 것이 남아 있으면 그 릴리즈는 깨진 것이다.
strays=$(find "$DIR" -type f ! -name 'MacroTyper.exe' ! -name '*.pdb' | wc -l | tr -d ' ')

if [ "$strays" != "0" ]; then
    echo >&2
    echo "exe 옆에 파일이 남았다. 이대로 올리면 받은 사람은 실행하지 못한다:" >&2
    find "$DIR" -type f ! -name 'MacroTyper.exe' ! -name '*.pdb' >&2
    exit 1
fi

cp "$DIR/MacroTyper.exe" "$OUT/$NAME"

# 예전에 함께 내던 작은 exe 가 남아 있으면 치운다. 실수로 다시 올리지 않게 한다.
rm -f "$OUT/MacroTyper-win-x64.exe"
rm -rf publish/win-x64-lite

echo
ls -lh "$OUT/$NAME"
echo
echo "다음: scripts/sign-windows.sh $OUT/$NAME"
