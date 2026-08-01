#!/usr/bin/env bash
#
# 릴리즈에 올릴 exe 두 개를 만든다.
#
#   scripts/publish-release.sh
#
# 이 스크립트를 거치지 않고 손으로 publish 하지 않는다. 자동 업데이트가 여기에 매여 있다.
#
#   - 파일 이름: 돌고 있는 예전 버전들이 이 이름을 찾는다. 바꾸면 그들은 새 버전을 못 받는다.
#     (UpdatePlan.AssetNameFor 와 같아야 한다)
#   - --self-contained: 이 값이 어셈블리에 새겨져서, 나중에 어느 exe 를 받을지 정한다.
#     빠뜨리면 standalone 을 쓰던 사람이 런타임 없는 lite 를 받아 실행되지 않는다.
#
# 만든 뒤에는 서명한다. 서명이 없으면 그 버전을 받은 사람들은 그다음 업데이트를 할 수 없다.
# 새 파일이 우리 것인지 대조할 기준이 사라지기 때문이다.
#
#   scripts/sign-windows.sh publish/release/*.exe

set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT="src/MacroTyper/MacroTyper.csproj"
OUT="publish/release"

# UpdatePlan.AssetNameFor 와 반드시 같아야 한다.
LITE_NAME="MacroTyper-win-x64.exe"
STANDALONE_NAME="MacroTyper-win-x64-standalone.exe"

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

publish() {
    local self_contained="$1" dir="$2" name="$3"

    rm -rf "$dir"

    # 압축을 켜면 standalone 이 147MB 에서 68MB 로 줄어든다.
    # 시작할 때 푸느라 몇백 ms 를 더 쓰지만, 업데이트마다 그만큼을 다시 받는 편이 더 비싸다.
    # lite 에는 켤 수 없다. 압축은 런타임을 품은 배포에서만 지원된다.
    local compress=false
    [ "$self_contained" = "true" ] && compress=true

    dotnet publish "$PROJECT" \
        -c Release -r win-x64 \
        --self-contained "$self_contained" \
        -p:PublishSingleFile=true \
        -p:EnableCompressionInSingleFile="$compress" \
        -o "$dir"

    cp "$dir/MacroTyper.exe" "$OUT/$name"
}

publish false publish/win-x64-lite       "$LITE_NAME"
publish true  publish/win-x64-standalone "$STANDALONE_NAME"

echo
ls -lh "$OUT"/*.exe
echo
echo "다음: scripts/sign-windows.sh $OUT/*.exe"
