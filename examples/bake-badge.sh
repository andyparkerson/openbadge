#!/bin/bash

# Sample cURL request to bake a badge
# Usage: ./bake-badge.sh badge-template.png

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <png-file>"
    exit 1
fi

PNG_FILE=$1
ENDPOINT="${FUNCTION_ENDPOINT:-http://localhost:7071/api/bake}"

if [ ! -f "$PNG_FILE" ]; then
    echo "Error: PNG file not found: $PNG_FILE"
    exit 1
fi

echo "Baking badge into $PNG_FILE..."

curl -X POST "$ENDPOINT" \
  -H "Content-Type: multipart/form-data" \
  -F "png=@$PNG_FILE" \
  -F 'json={
    "standard": "ob2",
    "award": {
      "issuer": {
        "id": "example-org",
        "name": "Example Organization",
        "url": "https://example.org",
        "email": "badges@example.org",
        "description": "An example organization that issues badges"
      },
      "badgeClass": {
        "id": "awesome-achiever",
        "name": "Awesome Achiever",
        "description": "Awarded to individuals who demonstrate awesome achievements",
        "image": "https://example.org/images/awesome-badge.png",
        "issuer": "example-org",
        "criteria": [
          "Completed the awesome training program",
          "Demonstrated mastery of awesome skills",
          "Contributed to the awesome community"
        ],
        "tags": ["achievement", "learning", "awesome"]
      },
      "recipient": {
        "type": "email",
        "identity": "learner@example.com",
        "hashed": false
      },
      "issuedOn": "2024-12-12T00:00:00Z",
      "evidence": "https://example.org/evidence/learning-portfolio"
    }
  }' | jq '.'

echo -e "\n✅ Badge baking request completed!"
