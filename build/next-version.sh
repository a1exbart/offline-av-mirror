#!/usr/bin/env bash
#
# What the next version would be, and the command that cuts it.
#
# The version lives in exactly one place — the git tag — and git-cliff works out
# the bump from the conventional commits since the previous one. Nothing here
# changes the repository; it prints what to run.
#
#   build/next-version.sh            what the next tag would be
#   build/next-version.sh --notes    and the release notes that would go with it
set -euo pipefail

command -v git-cliff > /dev/null || {
    echo "git-cliff is not installed: https://git-cliff.org" >&2
    exit 127
}

current="$(git describe --tags --abbrev=0 2>/dev/null || echo "none")"
next="$(git-cliff --bumped-version 2>/dev/null)"

echo "current: $current"
echo "next:    $next"

if [ "$current" = "$next" ]; then
    echo
    echo "Nothing to release: no commit since $current changes the version."
    exit 0
fi

if [ "${1:-}" = "--notes" ]; then
    echo
    git-cliff --unreleased --tag "$next" --strip all
fi

cat <<EOF

To release:
  git tag -a $next -m "$next" && git push origin $next

A candidate that is not meant as a finished version takes a SemVer pre-release
suffix and is published as a pre-release:
  git tag -a ${next}-rc.1 -m "${next}-rc.1" && git push origin ${next}-rc.1
EOF
