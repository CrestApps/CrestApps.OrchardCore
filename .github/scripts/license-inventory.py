#!/usr/bin/env python3
"""Generate a third-party license inventory for every package the solution resolves.

The inventory is built from data the build already produces rather than from a third-party scanner:
`dotnet list package --include-transitive --format json` supplies the resolved graph, and each package's
license is read from the `.nuspec` in the local NuGet cache. That keeps the inventory reproducible and
verifiable offline once the packages are restored.

Exits non-zero when a package resolves to no discoverable license, so an unreviewed dependency cannot
enter the graph unnoticed.
"""

import argparse
import json
import os
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ElementTree

# Packages produced by this repository are covered by the repository licence itself.
OWN_PACKAGE_PREFIXES = ("CrestApps.",)

# Packages that ship no licence metadata in their nuspec at all. Each entry records the licence found by
# reading the upstream repository the package itself names, so the inventory stays complete without
# weakening the gate. Remove an entry once the package starts declaring its licence.
REVIEWED_LICENSES = {
    "Aspire.Hosting.Elasticsearch": {
        "license": "Apache-2.0",
        "source": "https://github.com/elastic/elastic-aspire-dotnet (repository named in the package nuspec)",
    },
    "SQLitePCLRaw.lib.e_sqlite3": {
        "license": "Apache-2.0",
        "source": "https://github.com/ericsink/SQLitePCL.raw (upstream project of the SQLitePCLRaw packages)",
    },
}


def enumerate_packages(repository_root):
    """Return the distinct (id, version) pairs the solution resolves."""
    result = subprocess.run(
        ["dotnet", "list", "package", "--include-transitive", "--format", "json"],
        cwd=repository_root,
        capture_output=True,
        text=True,
        check=True,
    )

    document = json.loads(result.stdout)
    packages = set()

    for project in document.get("projects") or []:
        for framework in project.get("frameworks") or []:
            for key in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(key) or []:
                    version = package.get("resolvedVersion") or package.get("requestedVersion")

                    if package.get("id") and version:
                        packages.add((package["id"], version))

    return sorted(packages)


def read_license(packages_root, package_id, version):
    """Return the license metadata recorded in a package's nuspec, or None when it declares none."""
    nuspec = packages_root / package_id.lower() / version.lower() / f"{package_id.lower()}.nuspec"

    if not nuspec.is_file():
        return None

    try:
        root = ElementTree.parse(nuspec).getroot()
    except ElementTree.ParseError:
        return None

    namespace = ""

    if root.tag.startswith("{"):
        namespace = root.tag[: root.tag.index("}") + 1]

    metadata = root.find(f"{namespace}metadata")

    if metadata is None:
        return None

    def text(name):
        node = metadata.find(f"{namespace}{name}")

        return node.text.strip() if node is not None and node.text else None

    license_node = metadata.find(f"{namespace}license")
    expression = None
    license_file = None

    if license_node is not None and license_node.text:
        if license_node.get("type") == "file":
            license_file = license_node.text.strip()
        else:
            expression = license_node.text.strip()

    return {
        "id": package_id,
        "version": version,
        "license": expression,
        "licenseFile": license_file,
        "licenseUrl": text("licenseUrl"),
        "projectUrl": text("projectUrl"),
        "authors": text("authors"),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, help="Path of the JSON inventory to write.")
    parser.add_argument(
        "--packages-root",
        default=os.environ.get("NUGET_PACKAGES", str(pathlib.Path.home() / ".nuget" / "packages")),
        help="Root of the local NuGet package cache.",
    )
    arguments = parser.parse_args()

    repository_root = pathlib.Path(__file__).resolve().parents[2]
    packages_root = pathlib.Path(arguments.packages_root)
    entries = []
    unlicensed = []

    for package_id, version in enumerate_packages(repository_root):
        entry = read_license(packages_root, package_id, version)

        if entry is None:
            entry = {
                "id": package_id,
                "version": version,
                "license": None,
                "licenseFile": None,
                "licenseUrl": None,
                "projectUrl": None,
                "authors": None,
            }

        has_license = entry["license"] or entry["licenseFile"] or entry["licenseUrl"]

        if not has_license:
            reviewed = REVIEWED_LICENSES.get(package_id)

            if reviewed is not None:
                entry["license"] = reviewed["license"]
                entry["licenseSource"] = reviewed["source"]
                has_license = True

        entries.append(entry)

        is_own = any(package_id.startswith(prefix) for prefix in OWN_PACKAGE_PREFIXES)

        if not has_license and not is_own:
            unlicensed.append(f"{package_id} {version}")

    output = pathlib.Path(arguments.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"packages": entries}, indent=2) + "\n")

    print(f"Wrote {len(entries)} packages to {output}.")

    if unlicensed:
        print(f"::error::{len(unlicensed)} package(s) declare no license: {', '.join(sorted(unlicensed))}")

        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
