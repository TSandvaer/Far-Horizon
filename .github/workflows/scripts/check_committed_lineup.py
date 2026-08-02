#!/usr/bin/env python3
"""check_committed_lineup.py -- COMMITTED-BYTES drift guard for the weapon-set
lineup prefab, run in the hosted `structure` job (ticket 86cayp0p9).

WHAT THIS GUARDS (and what it deliberately does NOT)
----------------------------------------------------
`Assets/Resources/WeaponSetLineup.prefab` is a PROCEDURALLY GENERATED asset that
is nevertheless COMMITTED -- `WeaponPackAssetGen.BuildFamilyPrefab` bakes it from
the generator's row sets. Builds ship the COMMITTED snapshot, so a generator whose
output has drifted from the committed bytes ships the STALE asset silently
(unity-conventions.md, `[[unity-procedural-committed-assets-go-stale]]`, 86cahvntg).

There is ALREADY an EditMode guard for this asset --
`Assets/Tests/EditMode/CommittedLineupDriftGuardTests.cs` (86catwzhy). It is NOT
broken and is NOT superseded: it declares its own scope verbatim, namely that on CI
the `build` job runs `BootstrapProject.Run` (ci.yml `Bootstrap project` step) BEFORE
the EditMode step, which re-bakes the prefab ON DISK, so a COMMITTED-ONLY drift is
overwritten with generator-fresh bytes before that test reads it. It guards
committed-state HONESTY for raw-editor reads / reviewer diffs / local no-rebake
runs. The gap this script closes is the ABSENCE of any CI-RED path on the committed
artifact -- not a defect in that test, which cannot provide one from inside a job
that bootstraps.

WHY THE `structure` JOB
-----------------------
`BootstrapProject.Run` appears in ci.yml at exactly two step sites (the `build` job
and the `playmode` job). ANY job that bootstraps re-bakes the asset on disk before a
read, which is the whole failure mode. The hosted `structure` job never bootstraps
(and has no Unity at all), so a red here is UNCONDITIONAL. It is also hosted, so it
does not contend for the single self-hosted Unity build slot.

WHY `git show`, NOT A WORKING-TREE READ
---------------------------------------
`AssetDatabase.LoadAssetAtPath` post-bootstrap returns re-baked bytes, and a plain
working-tree read would pass on a dirty checkout that a reviewer never sees. This
script reads BOTH inputs -- the prefab AND the generator source -- out of the COMMIT
via `git show <ref>:<path>`, so it grades exactly the bytes that would merge and
that a build would ship.

WHY THE EXPECTED SET IS DERIVED, NOT HAND-COPIED
------------------------------------------------
A hand-copied node list drifts itself, and then the GUARD is the thing lying. The
`structure` job has no Unity and cannot evaluate C# consts, so this script PARSES
the generator's own source of truth out of `WeaponPackAssetGen.cs`:

  1. `public const string PrefabPath = "<path>";`  -> which asset to read
  2. `public const string Dir = "<dir>";`          -> the FBX directory
  3. `public const string <Name> = Dir + "/<file>.fbx";` -> const name -> fbx path
  4. the `AddRow(root.transform, <SetName>, ...)` calls INSIDE `BuildFamilyPrefab`
     -> which row sets actually land in the prefab
  5. `(string path, float x)[] <SetName> = { (<ConstName>, x), ... };`
     -> the const names each row set contributes

  expected node name = Path.GetFileNameWithoutExtension(fbxPath)   [the `AddRow`
  child-naming rule: `item.name = Path.GetFileNameWithoutExtension(path)`]

Deriving from the AddRow-referenced ROW SETS (not from "every `*FbxPath` const")
matters in both directions: a retired-but-still-declared const cannot manufacture a
false RED, and a row set silently dropped from `BuildFamilyPrefab` cannot manufacture
a false GREEN. No literal weapon name and no expected COUNT appears anywhere below.

The parse is self-checking: every structural step that could silently narrow the
expected set (missing method, zero AddRow calls, an unresolvable set or const,
an empty expected set, a node name outside the `wpn_` weapon-prefix convention)
is a LOUD exit 2, never a quiet pass. A guard that cannot read its inputs is a
failure, not a green.

COMPARISON
----------
Set-membership over the prefab root's DIRECT CHILDREN, resolved through the
serialized Transform hierarchy (`m_Father: {fileID: 0}` -> root; root's
`m_Children` -> child Transforms -> their GameObjects' `m_Name`). Depth matters:
a scan of every `m_Name` in the file would let a node nested one level deep
satisfy the check, and the generator's contract is that AddRow parents every
weapon DIRECTLY under the root.

  PRIMARY   -- every expected node present as a direct child (names the missing).
  SECONDARY -- no UNEXPECTED `wpn_`-prefixed direct child (catches a renamed or
              extra weapon node, which is committed drift too).

Non-weapon children (the generator's `StandLight`) are ignored by design.

Usage:
  check_committed_lineup.py [<git-ref>]      # default: HEAD

Exit 0 = committed bytes match the generator contract.
Exit 1 = DRIFT (the thing this guard exists to catch).
Exit 2 = parse / IO / usage failure (fail loud; never a silent pass).
"""
import posixpath
import re
import subprocess
import sys

GENERATOR_PATH = "Assets/Scripts/Editor/WeaponPackAssetGen.cs"

# The weapon-asset filename prefix from the pipeline naming convention
# (`blender-asset-pipeline.md` sec.1: `wpn_` / `prop_` / `env_`). Only used for the
# SECONDARY unexpected-node check, and asserted against the derived expected set
# below so a convention change surfaces loudly instead of silently under-checking.
WEAPON_PREFIX = "wpn_"

TAG = "[lineup-drift]"

RE_DIR_CONST = re.compile(r'\bconst\s+string\s+Dir\s*=\s*"([^"]*)"\s*;')
RE_PREFAB_CONST = re.compile(r'\bconst\s+string\s+PrefabPath\s*=\s*"([^"]*)"\s*;')
RE_FBX_CONST = re.compile(r'\bconst\s+string\s+(\w+)\s*=\s*Dir\s*\+\s*"([^"]*)"\s*;')
RE_ROWSET_DECL = re.compile(
    r'\(\s*string\s+path\s*,\s*float\s+x\s*\)\s*\[\]\s+(\w+)\s*=\s*\{(.*?)\}\s*;',
    re.S)
RE_ROWSET_MEMBER = re.compile(r'\(\s*(\w+)\s*,')
RE_ADDROW_CALL = re.compile(r'\bAddRow\s*\(\s*[^,()]+,\s*(\w+)\s*,')
RE_BUILD_METHOD = re.compile(r'\bvoid\s+BuildFamilyPrefab\s*\(')


class GuardError(Exception):
    """A parse/IO failure -- exit 2, never a silent pass."""


def git_show(ref, path):
    """Return the blob at <ref>:<path>, or raise GuardError."""
    spec = f"{ref}:{path}"
    proc = subprocess.run(["git", "show", spec],
                          capture_output=True, text=True)
    if proc.returncode != 0:
        raise GuardError(
            f"cannot read {spec} ({(proc.stderr or '').strip()}). This guard reads "
            "both inputs from the COMMIT -- a missing blob is a failure, not a pass.")
    return proc.stdout


def strip_literals(text):
    """Blank out string/char literal CONTENTS and // comments, preserving length.

    Used only for brace matching, so a `{`/`}` inside a string or a comment cannot
    mis-delimit a method body. Indices stay aligned with the original text.
    """
    out = list(text)
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c in ('"', "'"):
            quote = c
            i += 1
            while i < n and text[i] != quote:
                if text[i] == "\\":
                    out[i] = " "
                    i += 1
                    if i < n:
                        out[i] = " "
                        i += 1
                    continue
                out[i] = " "
                i += 1
            i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                out[i] = " "
                i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                out[i] = " "
                i += 1
            i += 2
        else:
            i += 1
    return "".join(out)


def method_body(src, name_re, label):
    """Return the brace-matched body of the first method matching name_re."""
    m = name_re.search(src)
    if not m:
        raise GuardError(
            f"could not find the {label} method in {GENERATOR_PATH}. The generator "
            "changed shape -- update this guard deliberately rather than letting it "
            "narrow silently.")
    blanked = strip_literals(src)
    start = blanked.find("{", m.end())
    if start < 0:
        raise GuardError(f"could not find the opening brace of {label}.")
    depth, i, n = 0, start, len(blanked)
    while i < n:
        if blanked[i] == "{":
            depth += 1
        elif blanked[i] == "}":
            depth -= 1
            if depth == 0:
                return src[start:i + 1]
        i += 1
    raise GuardError(f"unbalanced braces while reading {label}.")


def parse_generator(src):
    """Derive (prefab_path, expected_names, rows) from the generator source."""
    m = RE_PREFAB_CONST.search(src)
    if not m:
        raise GuardError(
            f"no `const string PrefabPath = \"...\";` in {GENERATOR_PATH} -- this "
            "guard takes the asset path from the generator, not from a literal.")
    prefab_path = m.group(1)

    if not RE_DIR_CONST.search(src):
        raise GuardError(
            f"no `const string Dir = \"...\";` in {GENERATOR_PATH} -- the FBX path "
            "consts are built from it, so its absence means the parse is stale.")
    dir_value = RE_DIR_CONST.search(src).group(1)

    # const name -> fbx asset path. Alias consts (e.g. `HeroAxeFbxPath = AxeFbxPath;`)
    # carry no `Dir + "..."` literal and are correctly absent here; they resolve
    # through the row sets only if a row set actually names them.
    const_paths = {name: dir_value + suffix
                   for name, suffix in RE_FBX_CONST.findall(src)}
    if not const_paths:
        raise GuardError(
            f"parsed ZERO `Dir + \"/...\"` path consts from {GENERATOR_PATH} -- the "
            "parse broke. Failing loud rather than declaring an empty contract met.")

    # Row-set declarations: name -> [const names]
    rowsets = {}
    for set_name, body in RE_ROWSET_DECL.findall(src):
        rowsets[set_name] = RE_ROWSET_MEMBER.findall(body)

    # Only the row sets BuildFamilyPrefab actually AddRow()s land in the prefab.
    body = method_body(src, RE_BUILD_METHOD, "BuildFamilyPrefab")
    used = []
    for set_name in RE_ADDROW_CALL.findall(body):
        if set_name not in used:
            used.append(set_name)
    if not used:
        raise GuardError(
            "found ZERO `AddRow(<parent>, <set>, ...)` calls inside "
            "BuildFamilyPrefab -- the parse broke, or the prefab is no longer built "
            "from row sets. Failing loud rather than expecting nothing.")

    expected, rows = [], []
    for set_name in used:
        members = rowsets.get(set_name)
        if not members:
            raise GuardError(
                f"BuildFamilyPrefab rows `{set_name}`, but no non-empty "
                f"`(string path, float x)[] {set_name} = {{...}}` declaration was "
                f"parsed from {GENERATOR_PATH}.")
        names = []
        for const_name in members:
            fbx = const_paths.get(const_name)
            if fbx is None:
                raise GuardError(
                    f"row set `{set_name}` names `{const_name}`, which does not "
                    f"resolve to a `Dir + \"/...\"` path const in {GENERATOR_PATH}. "
                    "An unresolvable member would silently shrink the expected set.")
            node = posixpath.splitext(posixpath.basename(fbx))[0]
            names.append(node)
            if node not in expected:
                expected.append(node)
        rows.append((set_name, names))

    if not expected:
        raise GuardError("derived an EMPTY expected node set -- refusing to pass.")

    off_convention = [n for n in expected if not n.startswith(WEAPON_PREFIX)]
    if off_convention:
        raise GuardError(
            f"derived node name(s) outside the `{WEAPON_PREFIX}` weapon-prefix "
            f"convention: {off_convention}. The unexpected-node check keys on that "
            "prefix, so a convention change must be handled deliberately here "
            "(blender-asset-pipeline.md sec.1) rather than silently under-checking.")

    return prefab_path, expected, rows


def parse_prefab_direct_children(text, prefab_path):
    """Return (root_name, [direct-child GameObject names]) from prefab YAML."""
    go_names = {}        # anchor -> m_Name        (class 1, GameObject)
    tf_owner = {}        # anchor -> GameObject anchor  (class 4, Transform)
    tf_father = {}       # anchor -> father Transform anchor
    tf_children = {}     # anchor -> [child Transform anchors]

    anchor = None
    cls = None
    in_children = False
    for raw in text.splitlines():
        m = re.match(r'^--- !u!(\d+) &(\d+)', raw)
        if m:
            cls, anchor = int(m.group(1)), m.group(2)
            in_children = False
            continue
        if anchor is None:
            continue
        if in_children:
            mc = re.match(r'^\s*-\s*\{fileID:\s*(-?\d+)\}', raw)
            if mc:
                tf_children.setdefault(anchor, []).append(mc.group(1))
                continue
            in_children = False
        if cls == 1:
            mn = re.match(r'^  m_Name:\s*(.*?)\s*$', raw)
            if mn:
                go_names[anchor] = mn.group(1)
        elif cls == 4:
            mo = re.match(r'^  m_GameObject:\s*\{fileID:\s*(-?\d+)\}', raw)
            if mo:
                tf_owner[anchor] = mo.group(1)
                continue
            mf = re.match(r'^  m_Father:\s*\{fileID:\s*(-?\d+)\}', raw)
            if mf:
                tf_father[anchor] = mf.group(1)
                continue
            if re.match(r'^  m_Children:\s*$', raw):
                tf_children.setdefault(anchor, [])
                in_children = True

    if not go_names:
        raise GuardError(
            f"parsed ZERO GameObject blocks from {prefab_path} -- not a Unity YAML "
            "prefab, or the serialization shape changed.")

    roots = [a for a in tf_father if tf_father[a] == "0"]
    if len(roots) != 1:
        raise GuardError(
            f"expected exactly ONE root Transform (m_Father: {{fileID: 0}}) in "
            f"{prefab_path}, found {len(roots)}. Refusing to guess the hierarchy.")
    root_tf = roots[0]
    root_go = tf_owner.get(root_tf)
    root_name = go_names.get(root_go, "<unnamed>")

    children = []
    for child_tf in tf_children.get(root_tf, []):
        owner = tf_owner.get(child_tf)
        if owner is None or owner not in go_names:
            raise GuardError(
                f"root child Transform fileID {child_tf} in {prefab_path} does not "
                "resolve to a named GameObject -- refusing to under-report children.")
        children.append(go_names[owner])
    return root_name, children


def main(argv):
    ref = argv[1] if len(argv) > 1 else "HEAD"
    if len(argv) > 2:
        print("usage: check_committed_lineup.py [<git-ref>]", file=sys.stderr)
        return 2

    try:
        gen_src = git_show(ref, GENERATOR_PATH)
        prefab_path, expected, rows = parse_generator(gen_src)
        prefab_txt = git_show(ref, prefab_path)
        root_name, children = parse_prefab_direct_children(prefab_txt, prefab_path)
    except GuardError as e:
        print(f"{TAG} ERROR -- {e}", file=sys.stderr)
        return 2

    rows_desc = " ".join(f"{n}({len(v)})" for n, v in rows)
    actual = set(children)
    missing = [n for n in expected if n not in actual]
    unexpected = sorted(n for n in actual
                        if n.startswith(WEAPON_PREFIX) and n not in expected)

    if missing:
        print(f"{TAG} FAILED -- committed {prefab_path} at ref '{ref}' is MISSING "
              f"{len(missing)} generator node(s): [{', '.join(missing)}]",
              file=sys.stderr)
        print(f"{TAG}   generator contract = {len(expected)} node(s) from "
              f"{rows_desc}; committed root '{root_name}' has "
              f"{len(children)} direct child(ren).", file=sys.stderr)
        print(f"{TAG}   This is COMMITTED-BYTES drift: the generator rows re-bake "
              "to the full set, but the committed prefab was not re-baked. Builds "
              "ship the COMMITTED snapshot, and CI's BootstrapProject.Run re-bake "
              "masks this everywhere except here. Fix by re-baking: Unity -batchmode "
              "-quit -executeMethod "
              "FarHorizon.EditorTools.WeaponPackAssetGen.PrepareWeaponPack, then "
              "commit the regenerated prefab.", file=sys.stderr)
        return 1

    if unexpected:
        print(f"{TAG} FAILED -- committed {prefab_path} at ref '{ref}' has "
              f"{len(unexpected)} UNEXPECTED weapon node(s) not in the generator "
              f"contract: [{', '.join(unexpected)}]", file=sys.stderr)
        print(f"{TAG}   generator contract = {len(expected)} node(s) from "
              f"{rows_desc}. A renamed or extra weapon node is committed drift too "
              "-- re-bake (PrepareWeaponPack) and commit the result.",
              file=sys.stderr)
        return 1

    print(f"{TAG} OK -- committed {prefab_path} at ref '{ref}' carries all "
          f"{len(expected)} generator node(s) as direct children of '{root_name}'.")
    print(f"{TAG}   rows: {rows_desc}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
