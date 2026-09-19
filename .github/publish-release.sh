#!/usr/bin/env bash
#
# Create the release and attach the built files.
#
# The tag itself is created by this call: both forges create a tag at
# target_commitish when the release names one that does not exist yet, so CI
# never pushes to the repository.
#
# GitHub and Gitea differ exactly where it matters — GitHub uploads assets to a
# separate host with a raw body, Gitea takes multipart on the same host — so
# each gets its own path here and the workflow file stays readable.
#
# Only curl and python3 are used: jq is not in every runner image, python3 is.
# Nothing here relies on curl newer than 7.74 either (no --fail-with-body).
set -euo pipefail

version="${VERSION:?no version}"
prerelease="${PRERELEASE:-true}"
repo="${GITHUB_REPOSITORY:?no repository}"
sha="${GITHUB_SHA:?no commit}"
api="${GITHUB_API_URL:-https://api.github.com}"
token="${TOKEN:?no token}"
notes="artifacts/release-notes.md"

case "$api" in
    *api.github.com*) flavour="github" ;;
    *)                flavour="gitea" ;;
esac
base="$api/repos/$repo"

echo "publishing $version to $flavour ($api), prerelease=$prerelease"

# post <url> <curl args…> — fails loudly with the server's own answer, which is
# the only thing that ever explains a 401 or a 403.
post() {
    local url="$1" body code
    shift
    body="$(mktemp)"
    code="$(curl -sS -o "$body" -w '%{http_code}' -X POST \
        -H "Authorization: token $token" "$@" "$url")"
    if [ "$code" -ge 400 ]; then
        echo "POST $url -> HTTP $code" >&2
        cat "$body" >&2
        rm -f "$body"
        return 1
    fi
    cat "$body"
    rm -f "$body"
}

payload="$(python3 - "$version" "$notes" "$prerelease" "$sha" <<'PY'
import json, sys
version, notes, prerelease, sha = sys.argv[1:5]
with open(notes, encoding="utf-8") as f:
    body = f.read()
print(json.dumps({
    "tag_name": version,
    "target_commitish": sha,
    "name": version,
    "body": body,
    "prerelease": prerelease == "true",
    "draft": False,
}))
PY
)"

release="$(post "$base/releases" \
    -H "Content-Type: application/json" -H "Accept: application/json" -d "$payload")"
id="$(printf '%s' "$release" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
echo "release $id created"

for file in artifacts/release/*; do
    name="$(basename "$file")"
    if [ "$flavour" = "github" ]; then
        upload="${base/api.github.com/uploads.github.com}"
        post "$upload/releases/$id/assets?name=$name" \
            -H "Content-Type: application/octet-stream" --data-binary "@$file" > /dev/null
    else
        post "$base/releases/$id/assets?name=$name" -F "attachment=@$file" > /dev/null
    fi
    echo "attached $name"
done

echo "released $version"
