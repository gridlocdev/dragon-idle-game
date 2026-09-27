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

# Delete build output
clean:
    rm -rf bin obj
