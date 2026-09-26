#!/usr/bin/env bash
# Builds and runs the iOS tracking core's host tests on a Mac.
# Usage: Tests/Tracking/Tools/run_native_tests.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
OUT="${TMPDIR:-/tmp}/holokit-tracking-tests"
clang++ -std=c++17 -O2 -Wall -Wextra -Werror \
  -I "$ROOT/Runtime/iOS/NativeCode" \
  "$ROOT/Runtime/iOS/NativeCode/HoloKitTrackingCore.cpp" \
  "$ROOT/Tests/Tracking/Native~/tracking_core_tests.cpp" -o "$OUT"
"$OUT" "$ROOT/Tests/Tracking/Native~/fixtures"
