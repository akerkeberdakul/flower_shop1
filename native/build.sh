#!/bin/sh
set -e
cd "$(dirname "$0")"

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64)
    mkdir -p bin/linux-x64
    gcc -shared -fPIC -O2 -Wall -Wextra -o bin/linux-x64/libflowerlib.so flowerlib.c
    echo "Built native/bin/linux-x64/libflowerlib.so"
    ;;
  *)
    echo "Unsupported platform for this script: $(uname -s)-$(uname -m). Supported: Linux x86_64 (this script), Windows x64 (build.cmd)."
    exit 1
    ;;
esac
