#!/usr/bin/env bash
#
# Create or update the release for this tag and attach the built files.
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
# Nothing here needs curl newer than 7.74 either (no --fail-with-body).
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

# send <method> <url> <curl args…> — fails loudly with the server's own answer,
# which is the only thing that ever explains a 401 or a 403.
send() {
    local method="$1" url="$2" body code
    shift 2
    body="$(mktemp)"
    code="$(curl -sS -o "$body" -w '%{http_code}' -X "$method" \
        -H "Authorization: token $token" "$@" "$url")"
    if [ "$code" -ge 400 ]; then
        echo "$method $url -> HTTP $code" >&2
        cat "$body" >&2
        rm -f "$body"
        return 1
    fi
    cat "$body"
    rm -f "$body"
}

# get <url> — the body, or nothing at all when the server says it is not there.
get() {
    local body code
    body="$(mktemp)"
    code="$(curl -sS -o "$body" -w '%{http_code}' -H "Authorization: token $token" "$1")"
    [ "$code" = "200" ] && cat "$body"
    rm -f "$body"
    return 0
}

payload="$(RELEASE_VERSION="$version" RELEASE_NOTES="$notes" RELEASE_PRE="$prerelease" RELEASE_SHA="$sha" \
    python3 -c '
import json, os
with open(os.environ["RELEASE_NOTES"], encoding="utf-8") as f:
    body = f.read()
print(json.dumps({
    "tag_name": os.environ["RELEASE_VERSION"],
    "target_commitish": os.environ["RELEASE_SHA"],
    "name": os.environ["RELEASE_VERSION"],
    "body": body,
    "prerelease": os.environ["RELEASE_PRE"] == "true",
    "draft": False,
}))')"

# A tag can arrive again — after a history rewrite, for instance — and then the
# release for it already exists. Update it instead of failing, so what the
# release says always matches the tag that is there now.
existing="$(get "$base/releases/tags/$version")"
if [ -n "$existing" ]; then
    id="$(printf '%s' "$existing" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
    send PATCH "$base/releases/$id" -H "Content-Type: application/json" -d "$payload" > /dev/null
    echo "release $id updated"
else
    id="$(send POST "$base/releases" \
        -H "Content-Type: application/json" -H "Accept: application/json" -d "$payload" \
        | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
    echo "release $id created"
fi

for file in artifacts/release/*; do
    name="$(basename "$file")"

    # Replace an asset of the same name: a rebuilt binary must never end up
    # hidden behind the previous one.
    old="$(get "$base/releases/$id/assets" | ASSET_NAME="$name" python3 -c '
import json, os, sys
data = sys.stdin.read().strip()
assets = json.loads(data) if data else []
print(next((str(a["id"]) for a in assets if a["name"] == os.environ["ASSET_NAME"]), ""))')"
    if [ -n "$old" ]; then
        send DELETE "$base/releases/assets/$old" > /dev/null
        echo "replaced $name"
    fi

    if [ "$flavour" = "github" ]; then
        upload="${base/api.github.com/uploads.github.com}"
        send POST "$upload/releases/$id/assets?name=$name" \
            -H "Content-Type: application/octet-stream" --data-binary "@$file" > /dev/null
    else
        send POST "$base/releases/$id/assets?name=$name" -F "attachment=@$file" > /dev/null
    fi
    echo "attached $name"
done

echo "released $version"
