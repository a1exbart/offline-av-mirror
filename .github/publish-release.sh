#!/usr/bin/env bash
#
# Create the release for the tag that triggered the workflow and attach the
# built files.
#
# GitHub and Gitea expose almost the same REST API, and they differ exactly
# where it matters: GitHub uploads an asset to a separate host with a raw body,
# Gitea takes it as multipart on the same host. Both paths live here so the
# workflow file stays readable.
#
# Only curl and python3 are used: jq is not in every runner image, python3 is.
set -euo pipefail

tag="${GITHUB_REF_NAME:?no tag}"
repo="${GITHUB_REPOSITORY:?no repository}"
api="${GITHUB_API_URL:-https://api.github.com}"
notes="artifacts/release-notes.md"
token="${TOKEN:?no token}"

# A SemVer pre-release (v1.2.3-rc.1) is published as a pre-release. Nothing else
# distinguishes a candidate from a finished version.
prerelease=false
case "$tag" in *-*) prerelease=true ;; esac

case "$api" in
    *api.github.com*) flavour="github" ;;
    *)                flavour="gitea" ;;
esac

echo "publishing $tag to $flavour ($api), prerelease=$prerelease"

payload="$(python3 - "$tag" "$notes" "$prerelease" <<'PY'
import json, sys
tag, notes, prerelease = sys.argv[1], sys.argv[2], sys.argv[3] == "true"
with open(notes, encoding="utf-8") as f:
    body = f.read()
print(json.dumps({
    "tag_name": tag,
    "name": tag,
    "body": body,
    "prerelease": prerelease,
}))
PY
)"

base="$api/repos/$repo"
release="$(curl -sS --fail-with-body -X POST \
    -H "Authorization: token $token" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    -d "$payload" "$base/releases")"

id="$(printf '%s' "$release" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
echo "release $id created"

for file in artifacts/release/*; do
    name="$(basename "$file")"
    if [ "$flavour" = "github" ]; then
        upload="${base/api.github.com/uploads.github.com}"
        curl -sS --fail-with-body -X POST \
            -H "Authorization: token $token" \
            -H "Content-Type: application/octet-stream" \
            --data-binary "@$file" \
            "$upload/releases/$id/assets?name=$name" > /dev/null
    else
        curl -sS --fail-with-body -X POST \
            -H "Authorization: token $token" \
            -F "attachment=@$file" \
            "$base/releases/$id/assets?name=$name" > /dev/null
    fi
    echo "attached $name"
done

echo "released $tag"
