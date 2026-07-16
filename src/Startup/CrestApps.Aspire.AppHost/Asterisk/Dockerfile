# Owned local WebRTC Asterisk image for the single-node Contact Center profile.
# TODO(ci): replace the base reference below with a digest-pinned form:
# FROM asterisk/asterisk:22.6.0@sha256:<digest>
FROM asterisk/asterisk:22.6.0

USER root

RUN mkdir -p /etc/asterisk/keys \
    && openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
        -keyout /etc/asterisk/keys/asterisk.key \
        -out /etc/asterisk/keys/asterisk.pem \
        -subj "/CN=localhost" \
    && chown -R asterisk:asterisk /etc/asterisk/keys \
    && chmod 600 /etc/asterisk/keys/asterisk.key \
    && chmod 644 /etc/asterisk/keys/asterisk.pem

USER asterisk
