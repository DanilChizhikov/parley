"""Packs the package sources into a .unitypackage without Unity.

A .unitypackage is a gzipped tar with one folder per asset, named by the asset's GUID, holding `asset.meta`, `pathname`
(the project path the asset is imported to) and, for files, `asset`. Every file and folder must have its .meta next to
it: the GUIDs are taken from there, so a missing .meta, a .meta without an asset or a duplicate GUID fails the build.

Usage: python3 build-unitypackage.py <package-root> <asset-path> <output>
  e.g. python3 build-unitypackage.py . Assets/Parley com.dtech.parley.1.0.0.unitypackage
"""

import gzip
import io
import os
import re
import sys
import tarfile

EXCLUDED_ROOT_ENTRIES = {".git", ".github", ".gitignore", ".gitattributes", "Documentation~"}
GUID_PATTERN = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)


def fail(message):
    print(f"::error::{message}")
    sys.exit(1)


def collect_assets(root, output):
    assets = []
    orphan_metas = []
    for directory, folders, files in os.walk(root):
        relative_directory = os.path.relpath(directory, root)
        is_root = relative_directory == "."
        folders[:] = sorted(
            name for name in folders
            if not name.startswith(".") and not (is_root and name in EXCLUDED_ROOT_ENTRIES)
        )

        names = set(files) | set(folders)
        for name in sorted(names):
            path = os.path.join(directory, name)
            if name.startswith(".") or (is_root and name in EXCLUDED_ROOT_ENTRIES):
                continue
            if os.path.abspath(path) == output:
                continue
            if name.endswith(".meta"):
                if name[:-len(".meta")] not in names:
                    orphan_metas.append(os.path.relpath(path, root))
                continue
            assets.append(os.path.relpath(path, root))

    return assets, orphan_metas


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as meta:
        match = GUID_PATTERN.search(meta.read())

    return match.group(1) if match else None


def add_bytes(archive, name, data, mode=0o644):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = mode
    info.mtime = 0
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    archive.addfile(info, io.BytesIO(data))


def add_folder(archive, name):
    info = tarfile.TarInfo(name)
    info.type = tarfile.DIRTYPE
    info.mode = 0o755
    info.mtime = 0
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    archive.addfile(info)


def main():
    if len(sys.argv) != 4:
        fail("Usage: build-unitypackage.py <package-root> <asset-path> <output>")

    root = os.path.abspath(sys.argv[1])
    asset_path = sys.argv[2].strip("/")
    output = os.path.abspath(sys.argv[3])

    assets, orphan_metas = collect_assets(root, output)

    missing_metas = [path for path in assets if not os.path.isfile(os.path.join(root, path + ".meta"))]
    if missing_metas:
        fail("Assets without a .meta file: " + ", ".join(missing_metas))

    if orphan_metas:
        fail(".meta files without an asset: " + ", ".join(orphan_metas))

    guids = {}
    for path in assets:
        guid = read_guid(os.path.join(root, path + ".meta"))
        if guid is None:
            fail(f"{path}.meta has no guid.")
        if guid in guids:
            fail(f"{path}.meta and {guids[guid]}.meta share the guid {guid}.")
        guids[guid] = path

    with gzip.GzipFile(output, "wb", mtime=0) as packed, \
            tarfile.open(fileobj=packed, mode="w", format=tarfile.GNU_FORMAT) as archive:
        for guid, path in sorted(guids.items(), key=lambda item: item[1]):
            source = os.path.join(root, path)
            add_folder(archive, guid)
            with open(source + ".meta", "rb") as meta:
                add_bytes(archive, f"{guid}/asset.meta", meta.read())
            pathname = f"{asset_path}/{path.replace(os.sep, '/')}"
            add_bytes(archive, f"{guid}/pathname", pathname.encode("utf-8"))
            if os.path.isfile(source):
                with open(source, "rb") as asset:
                    add_bytes(archive, f"{guid}/asset", asset.read())

    print(f"Packed {len(guids)} assets into {output}")


if __name__ == "__main__":
    main()
