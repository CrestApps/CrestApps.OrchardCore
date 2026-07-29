# Owned local WebRTC Asterisk image for the single-node Contact Center profile.
#
# The base is pinned by digest so the image is reproducible and so the supply chain scan reports on the
# exact bytes that were scanned. Update the tag comment and the digest together.
#
# This image restates the base ENTRYPOINT/CMD so the entrypoint below can wrap them. When the digest is
# bumped, re-check the base with `docker inspect` and update the CMD here if it changed.
#
# Previously this referenced `asterisk/asterisk:22.6.0`, which does not exist on Docker Hub - the registry
# answers "object not found" for that repository - so this image could never have been built.
#
# andrius/asterisk:22.10.1_debian-trixie
FROM andrius/asterisk@sha256:4cfb208f877b45e88115b35140b1edba06e1374e9bce4b6f5e55a84e60254b92

USER root

# The WebRTC certificate is deliberately NOT generated here. Writing it into an image layer would bake a
# private key into the published image, so the entrypoint creates it at container start instead.
RUN mkdir -p /etc/asterisk/keys \
    && chown -R asterisk:asterisk /etc/asterisk/keys \
    && chmod 700 /etc/asterisk/keys

COPY --chown=asterisk:asterisk docker-entrypoint.sh /usr/local/bin/crestapps-entrypoint.sh
RUN chmod 755 /usr/local/bin/crestapps-entrypoint.sh

USER asterisk

ENTRYPOINT ["/usr/local/bin/crestapps-entrypoint.sh"]
CMD ["/usr/sbin/asterisk", "-vvvdddf", "-T", "-W", "-U", "asterisk", "-p"]
