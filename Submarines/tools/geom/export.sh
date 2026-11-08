#!/usr/bin/env bash
# Exports the procedural submarine models to OBJ (mono + mcs), for previewing in Blender etc.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/../../models}"
mkdir -p "$out"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
mcs -nologo -target:library -out:"$tmp/UnityEngine.dll" "$here/../stubs/Unity.cs"
mcs -nologo -target:exe -r:"$tmp/UnityEngine.dll" -out:"$tmp/export.exe" "$here/ExportObj.cs" "$here/../../src/Visual/HullGeometry.cs"
(cd "$tmp" && mono export.exe "$out")
