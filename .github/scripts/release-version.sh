#!/bin/sh
# Reads the release version from a release/X.Y.Z or hotfix/X.Y.Z branch and checks the pull request is ready to ship:
# package.json carries that version and its changelogUrl points at the vX.Y.Z tag, CHANGELOG.md has its section,
# the description (the release notes) is not empty and the tag does not exist yet. Writes `version` to $GITHUB_OUTPUT.
#
# Input comes through the environment only: HEAD_REF (the PR branch) and PR_BODY (the PR description).
set -e

PACKAGE=package.json

fail() {
	echo "::error::$1"
	exit 1
}

read_package() {
	python3 -c 'import json, sys; print(json.load(open(sys.argv[1], encoding="utf-8")).get(sys.argv[2], ""))' "$PACKAGE" "$1"
}

case "$HEAD_REF" in
	release/*) VERSION=${HEAD_REF#release/} ;;
	hotfix/*) VERSION=${HEAD_REF#hotfix/} ;;
	*) fail "'$HEAD_REF' is not a release/X.Y.Z or hotfix/X.Y.Z branch." ;;
esac

if ! printf '%s' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
	fail "'$VERSION' from the branch name is not a X.Y.Z version."
fi

PACKAGE_VERSION=$(read_package version) || fail "Could not read $PACKAGE."
if [ "$PACKAGE_VERSION" != "$VERSION" ]; then
	fail "\"version\" in $PACKAGE is '$PACKAGE_VERSION'; bump it to $VERSION."
fi

CHANGELOG_URL=$(read_package changelogUrl) || fail "Could not read $PACKAGE."
case "$CHANGELOG_URL" in
	*/releases/tag/v"$VERSION") ;;
	*) fail "\"changelogUrl\" in $PACKAGE is '$CHANGELOG_URL'; point it at /releases/tag/v$VERSION." ;;
esac

if ! grep -qF "## [$VERSION]" CHANGELOG.md; then
	fail "CHANGELOG.md has no '## [$VERSION]' section."
fi

if [ -z "$(printf '%s' "$PR_BODY" | tr -d '[:space:]')" ]; then
	fail "The pull request description is empty; it becomes the release notes of v$VERSION."
fi

TAG=$(git ls-remote --tags origin "refs/tags/v$VERSION") || fail "Could not read the tags of origin."
if [ -n "$TAG" ]; then
	fail "Tag v$VERSION already exists."
fi

echo "Release v$VERSION is ready."
if [ -n "$GITHUB_OUTPUT" ]; then
	echo "version=$VERSION" >> "$GITHUB_OUTPUT"
fi
