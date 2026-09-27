#!/usr/bin/env bash
# Builds a self-contained Windows x64 zip (no .NET install needed).
#
#   scripts/package-windows.sh [--out DIR]
#
# APP_VERSION overrides the <Version> from the .csproj (the release workflow sets it from the git tag).
#
# Output: DIR/dragon-idle-<version>-win-x64.zip, which unpacks to dragon-idle/.
# Only x64 is supported: raylib-cs ships no win-arm64 native library.
# Built as a WinExe so no console window opens alongside the game.
# Can be run from macOS, Linux or Windows (Git Bash). The .exe only gets the app icon embedded
# when built on Windows; elsewhere the SDK skips that step.
set -euo pipefail

cd "$(dirname "$0")/.."

PKG_NAME="dragon-idle"
EXE_NAME="DragonIdle"
PROJECT="DragonIdle.csproj"
ICON_PNG="assets/icon/AppIcon.png"
RID="win-x64"
OUT="dist"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) OUT="$2"; shift 2 ;;
    -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

VERSION="${APP_VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -1)}"
VERSION="${VERSION:-0.1.0}"
BUILD_DIR="build/windows"
STAGE="$BUILD_DIR/$PKG_NAME"
ARCHIVE="$OUT/$PKG_NAME-$VERSION-$RID.zip"

mkdir -p "$BUILD_DIR" "$OUT"
if [[ -e "$STAGE" ]]; then
  mv "$STAGE" "$BUILD_DIR/previous-$(date +%Y%m%d-%H%M%S)"
fi

echo "==> Publishing $RID"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
  -p:UseAppHost=true -p:OutputType=WinExe -p:Version="$VERSION" -p:DebugType=None -p:GenerateDocumentationFile=false \
  -o "$STAGE" --nologo -v quiet

cp "$ICON_PNG" "$STAGE/icon.png"

echo "==> Creating $ARCHIVE"
rm -f "$ARCHIVE"
if command -v zip >/dev/null; then
  # -X leaves out macOS/Unix extra attributes.
  (cd "$BUILD_DIR" && zip -qrX - "$PKG_NAME") > "$ARCHIVE"
elif command -v 7z >/dev/null; then
  archive_abs="$(cd "$OUT" && pwd)/$(basename "$ARCHIVE")"
  (cd "$BUILD_DIR" && 7z a -tzip -bso0 -bsp0 "$archive_abs" "$PKG_NAME")
else
  powershell -NoProfile -Command "Compress-Archive -Path '$BUILD_DIR/$PKG_NAME' -DestinationPath '$ARCHIVE' -Force"
fi

echo
echo "Built: $ARCHIVE  (v$VERSION, $(du -sh "$ARCHIVE" | cut -f1))"
echo "Run it by unzipping and opening $PKG_NAME\\$EXE_NAME.exe"
