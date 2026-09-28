#!/bin/sh
# Generates the self-signed WebRTC certificate at container start rather than at image build time.
#
# Building the certificate into an image layer bakes a private key into the published bytes, which the
# supply chain scan correctly reports as a leaked secret. Generating it here keeps the key inside the
# running container only, and gives every container a distinct key instead of one shared by every pull.
set -e

KEY_DIR=/etc/asterisk/keys

if [ ! -s "$KEY_DIR/asterisk.key" ]; then
    openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
        -keyout "$KEY_DIR/asterisk.key" \
        -out "$KEY_DIR/asterisk.pem" \
        -subj "/CN=localhost" >/dev/null 2>&1
    chmod 600 "$KEY_DIR/asterisk.key"
    chmod 644 "$KEY_DIR/asterisk.pem"
fi

exec /usr/local/bin/entrypoint.sh "$@"
