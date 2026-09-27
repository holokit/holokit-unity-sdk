#!/usr/bin/env bash
# Builds and runs the iOS hand depth core's host tests on a Mac.
# Usage: Tests/Hands/Tools~/run_native_tests.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
OUT="${TMPDIR:-/tmp}/holokit-hand-depth-tests"
clang++ -std=c++17 -O2 -Wall -Wextra -Werror \
  -I "$ROOT/Runtime/iOS/NativeCode" \
  "$ROOT/Runtime/iOS/NativeCode/HoloKitHandDepth.cpp" \
  "$ROOT/Tests/Hands/Native~/hand_depth_tests.cpp" -o "$OUT"
"$OUT"
