#!/bin/sh
set -e

BASE_URL="https://www.userbus.xyz/downloads/skysurf"

INSTALL_DIR="${HOME}/.local/bin"

# Detect platform -> RID suffix used in the asset names
OS=$(uname -s)
ARCH=$(uname -m)

if [ "$OS" = "Darwin" ] && [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
elif [ "$OS" = "Linux" ] && [ "$ARCH" = "x86_64" ]; then
    RID="linux-x64"
else
    echo "Unsupported platform: $OS/$ARCH" >&2
    echo "Supported: Linux x86_64, macOS arm64." >&2
    exit 1
fi

mkdir -p "$INSTALL_DIR"

# Each app: "<display name>|<command name>"
for entry in "Toms|toms" "Skysurf|skysurf"; do
    name=${entry%%|*}
    cmd=${entry##*|}
    asset="${cmd}-${RID}"
    dest="${INSTALL_DIR}/${cmd}"

    echo "Downloading ${name}..."
    curl -fsSL "${BASE_URL}/${asset}" -o "${dest}"
    chmod +x "${dest}"
    echo "Installed: ${dest}"
done

# Suggest PATH update if needed
case ":${PATH}:" in
    *":${INSTALL_DIR}:"*) ;;
    *)
        echo ""
        echo "Add this to your shell profile (~/.bashrc, ~/.zshrc, etc.) and restart your terminal:"
        echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
        ;;
esac

echo ""
echo "Ready. Run:  toms  or  skysurf"
