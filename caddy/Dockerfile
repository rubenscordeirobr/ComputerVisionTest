# Caddy with the Porkbun DNS module: the TLS certificate is obtained through a
# DNS-01 challenge (Porkbun API), because ports 80/443 on this host belong to
# other services and cannot be used for the usual HTTP/TLS-ALPN challenges.
FROM caddy:2-builder AS builder
RUN xcaddy build --with github.com/caddy-dns/porkbun

FROM caddy:2
COPY --from=builder /usr/bin/caddy /usr/bin/caddy
