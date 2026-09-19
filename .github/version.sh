#!/usr/bin/env bash
#
# Decide which version this run publishes, and write the release notes.
#
# Two ways in, one rule: the version is never stored in a file.
#
#   push of a tag   the tag is the version; a SemVer pre-release suffix
#                   (v1.2.3-rc.1) publishes it as a pre-release
#   push to main    git-cliff works out the bump from the conventional commits
#                   since the last tag; the result is published as a
#                   pre-release, because nobody has accepted it yet. No bump
#                   (only docs, chores or CI) means no release at all.
#
# Outputs for the workflow: release=true|false, version, prerelease.
set -euo pipefail

out="${GITHUB_OUTPUT:-/dev/stdout}"

archive="git-cliff-${CLIFF_VERSION}-x86_64-unknown-linux-gnu.tar.gz"
curl -sSfL -o "$archive" \
    "https://github.com/orhun/git-cliff/releases/download/v${CLIFF_VERSION}/${archive}"
echo "${CLIFF_SHA512}  ${archive}" | sha512sum -c -
tar xzf "$archive" --strip-components=1 -C /usr/local/bin "git-cliff-${CLIFF_VERSION}/git-cliff"
rm -f "$archive"

latest="$(git tag --sort=-v:refname | head -1)"
mkdir -p artifacts

if [ "${GITHUB_REF_TYPE:-branch}" = "tag" ]; then
    version="${GITHUB_REF_NAME}"
    case "$version" in *-*) prerelease=true ;; *) prerelease=false ;; esac
    git-cliff --latest --strip all > artifacts/release-notes.md
else
    version="$(git-cliff --bumped-version)"
    prerelease=true
    if [ "$version" = "$latest" ]; then
        echo "no bump since ${latest:-the beginning}: nothing to release"
        echo "release=false" >> "$out"
        exit 0
    fi
    git-cliff --unreleased --tag "$version" --strip all > artifacts/release-notes.md
fi

echo "version $version (previous: ${latest:-none}), prerelease=$prerelease"
cat artifacts/release-notes.md

{
    echo "release=true"
    echo "version=$version"
    echo "prerelease=$prerelease"
} >> "$out"
