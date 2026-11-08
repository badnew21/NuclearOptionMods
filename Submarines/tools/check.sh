#!/usr/bin/env bash
# Type-checks the mod with mono's mcs against hand-written stubs of the game API.
# This catches syntax and type errors without owning the game. It does NOT prove the real
# Assembly-CSharp matches: the stubs only encode the members this mod (and the reference
# mods it was modelled on) use. Build against the real game DLLs before shipping.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT
s="$here/stubs"
mcs -nologo -target:library -out:"$out/UnityEngine.dll" "$s/Unity.cs"
mcs -nologo -target:library -r:"$out/UnityEngine.dll" -out:"$out/BepInEx.dll" "$s/BepInEx.cs"
mcs -nologo -target:library -out:"$out/0Harmony.dll" "$s/Harmony.cs"
mcs -nologo -target:library -r:"$out/UnityEngine.dll" -out:"$out/Mirage.dll" "$s/Mirage.cs"
mcs -nologo -target:library -r:"$out/UnityEngine.dll" -r:"$out/Mirage.dll" -out:"$out/Assembly-CSharp.dll" "$s/Game.cs"
mcs -nologo -target:library -langversion:7.2 -warn:4 \
  -r:"$out/UnityEngine.dll" -r:"$out/BepInEx.dll" -r:"$out/0Harmony.dll" -r:"$out/Mirage.dll" -r:"$out/Assembly-CSharp.dll" \
  -out:"$out/Submarines.dll" $(find "$here/../src" -name '*.cs')
echo "Stub type-check passed."
