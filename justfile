# Dragon Idle dev recipes. Run `just` to list them.

default:
    @just --list

# Run the game
run:
    dotnet run

# Debug build
build:
    dotnet build

# Run with a late-game save that never touches your real slots
demo:
    dotnet run -- --demo

# Play the scripted screenshot tour and refresh docs/screenshots and docs/icons (macOS: uses sips)
shots:
    #!/usr/bin/env bash
    set -euo pipefail
    tmp="$(mktemp -d)"
    DRAGON_SHOTS="$tmp" dotnet run
    mkdir -p docs/screenshots
    for f in "$tmp"/*.png; do
        sips -s format jpeg -s formatOptions 82 -Z 1280 "$f" --out "docs/screenshots/$(basename "${f%.png}").jpg" >/dev/null
    done
    mkdir -p docs/icons
    cp "$tmp"/icons/*.png docs/icons/
    rm -rf "$tmp"
    echo "Screenshots written to docs/screenshots/, currency icons to docs/icons/"

# Re-render the app icon (PNG + Windows .ico) from the in-game dragon
icon:
    dotnet run -- --render-icon assets/icon/AppIcon.png

# Package "Dragon Idle.app" for this Mac's architecture into dist/
build-macos:
    scripts/package-mac.sh

# Package a universal (Apple Silicon + Intel) .app into dist/
build-macos-universal:
    scripts/package-mac.sh --arch universal

# Package a self-contained Linux x64 tarball into dist/ (works from macOS too)
build-linux:
    scripts/package-linux.sh

# Package a self-contained Windows x64 zip into dist/ (works from macOS or Linux; the .exe icon is only embedded on Windows)
build-windows:
    scripts/package-windows.sh

# Package and launch the .app
run-macos: build-macos
    open "dist/Dragon Idle.app"

# Build the .app and move it into ~/Applications (an older copy goes to the Trash)
install-macos: build-macos
    #!/usr/bin/env bash
    set -euo pipefail
    src="dist/Dragon Idle.app"
    dest="$HOME/Applications/Dragon Idle.app"
    mkdir -p "$HOME/Applications"
    if [[ -e "$dest" ]]; then trash "$dest"; fi
    mv "$src" "$dest"
    echo "Installed to $dest"

# Move the installed app from ~/Applications to the Trash (your saves are kept)
uninstall-macos:
    #!/usr/bin/env bash
    set -euo pipefail
    dest="$HOME/Applications/Dragon Idle.app"
    if [[ -e "$dest" ]]; then trash "$dest" && echo "Moved $dest to the Trash"; else echo "Not installed"; fi

# Delete build output
clean:
    rm -rf bin obj build dist
