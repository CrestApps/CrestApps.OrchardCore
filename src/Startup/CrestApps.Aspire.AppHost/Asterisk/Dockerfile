# Owned local WebRTC Asterisk image for the single-node Contact Center profile.
#
# The base is pinned by digest so the image is reproducible and so the supply chain scan reports on the
# exact bytes that were scanned. Update the tag comment and the digest together.
#
# Previously this referenced `asterisk/asterisk:22.6.0`, which does not exist on Docker Hub - the registry
# answers "object not found" for that repository - so this image could never have been built.
#
# andrius/asterisk:22.10.1_debian-trixie
FROM andrius/asterisk@sha256:4cfb208f877b45e88115b35140b1edba06e1374e9bce4b6f5e55a84e60254b92

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
