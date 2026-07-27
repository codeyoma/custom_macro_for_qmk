#!/usr/bin/env bash
#
# Windows exe 에 코드 서명을 붙인다. 맥에서 돈다(signtool 은 Windows 전용이라 osslsigncode 를 쓴다).
#
#   brew install osslsigncode
#   scripts/sign-windows.sh publish/release/MacroTyper-win-x64.exe
#
# 인증서는 저장소에 두지 않는다. 개인 키가 새어 나가면 누구나 이 이름으로 서명할 수 있다.
# 기본 위치는 ~/.macrotyper-signing 이고, 없으면 아래 안내대로 한 번 만들면 된다.
#
# 자체 서명이라 SmartScreen 경고 자체는 사라지지 않는다.
# 쓰는 PC 에 codeyoma-publisher.crt 를 신뢰할 수 있는 루트로 설치했을 때만 경고가 없어진다.

set -euo pipefail

CERT_DIR="${MACROTYPER_SIGNING_DIR:-$HOME/.macrotyper-signing}"
CERT="$CERT_DIR/cert.pem"
KEY="$CERT_DIR/key.pem"

TIMESTAMP_URL="${MACROTYPER_TSA:-http://timestamp.digicert.com}"
PRODUCT_NAME="MacroTyper"
PRODUCT_URL="https://github.com/codeyoma/custom_macro_for_qmk"

if [ $# -lt 1 ]; then
    echo "사용법: $0 <exe> [<exe> ...]" >&2
    exit 1
fi

if [ ! -f "$CERT" ] || [ ! -f "$KEY" ]; then
    cat >&2 <<EOF
인증서가 없다: $CERT_DIR

한 번만 만들면 된다:

  mkdir -p "$CERT_DIR" && chmod 700 "$CERT_DIR"
  openssl req -x509 -newkey rsa:4096 \\
    -keyout "$CERT_DIR/key.pem" -out "$CERT_DIR/cert.pem" \\
    -days 1095 -nodes \\
    -subj "/CN=codeyoma/O=codeyoma/C=KR" \\
    -addext "basicConstraints=critical,CA:FALSE" \\
    -addext "keyUsage=critical,digitalSignature" \\
    -addext "extendedKeyUsage=critical,codeSigning" \\
    -addext "subjectKeyIdentifier=hash"
  chmod 600 "$CERT_DIR/key.pem"

배포용 인증서(개인 키 없음)는 이렇게 뽑는다:

  openssl x509 -in "$CERT_DIR/cert.pem" -outform DER -out codeyoma-publisher.crt
EOF
    exit 1
fi

for target in "$@"; do
    if [ ! -f "$target" ]; then
        echo "파일이 없다: $target" >&2
        exit 1
    fi

    tmp="$target.signing"

    # 타임스탬프를 반드시 붙인다. 없으면 인증서가 만료되는 순간 기존 서명까지 무효가 된다.
    osslsigncode sign \
        -certs "$CERT" -key "$KEY" \
        -n "$PRODUCT_NAME" \
        -i "$PRODUCT_URL" \
        -ts "$TIMESTAMP_URL" \
        -h sha256 \
        -in "$target" -out "$tmp"

    mv "$tmp" "$target"

    osslsigncode verify -in "$target" -CAfile "$CERT" >/dev/null 2>&1 \
        && echo "서명 완료: $target" \
        || { echo "서명은 붙었으나 검증에 실패했다: $target" >&2; exit 1; }
done

echo
echo "SHA256:"
shasum -a 256 "$@"
