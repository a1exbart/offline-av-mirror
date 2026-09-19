#!/usr/bin/env bash
#
# Create the release and attach the artifacts.
#
# GitHub and Gitea expose almost the same REST API, and the differences are
# exactly where it matters: Gitea takes the asset name as a query parameter and
# the file as multipart, GitHub takes it in the URL with the raw body. Both are
# handled here so the workflow file stays readable.
set -euo pipefail

tag="${GITHUB_REF_NAME:?no tag}"
repo="${GITHUB_REPOSITORY:?no repository}"
api="${GITHUB_API_URL:-https://api.github.com}"
notes="artifacts/release-notes.md"
assets=(artifacts/release/*)

auth=(-H "Authorization: token ${TOKEN:?no token}")

case "$api" in
    *api.github.com*) flavour="github" ;;
    *)                flavour="gitea" ;;
esac

echo "publishing $tag to $flavour ($api)"

if [ "$flavour" = "github" ]; then
    base="$api/repos/$repo"
    body="$(jq -Rs . < "$notes")"
    id="$(curl -sS --fail-with-body -X POST "${auth[@]}" \
        -H "Accept: application/vnd.github+json" \
        -d "{\"tag_name\":\"$tag\",\"name\":\"$tag\",\"body\":$body}" \
        "$base/releases" | jq -r .id)"

    upload="${base/api.github.com/uploads.github.com}"
    for file in "${assets[@]}"; do
        curl -sS --fail-with-body -X POST "${auth[@]}" \
            -H "Content-Type: application/octet-stream" \
            --data-binary "@$file" \
            "$upload/releases/$id/assets?name=$(basename "$file")" > /dev/null
        echo "attached $(basename "$file")"
    done
else
    base="$api/repos/$repo"
    payload="$(jq -n --arg tag "$tag" --rawfile body "$notes" \
        '{tag_name:$tag, name:$tag, body:$body}')"
    id="$(curl -sS --fail-with-body -X POST "${auth[@]}" \
        -H "Content-Type: application/json" -d "$payload" \
        "$base/releases" | jq -r .id)"

    for file in "${assets[@]}"; do
        curl -sS --fail-with-body -X POST "${auth[@]}" \
            -F "attachment=@$file" \
            "$base/releases/$id/assets?name=$(basename "$file")" > /dev/null
        echo "attached $(basename "$file")"
    done
fi

echo "released $tag"
