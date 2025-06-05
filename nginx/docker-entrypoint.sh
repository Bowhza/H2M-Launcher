#!/bin/sh

echo 'Replacing env vars...'

envsubst '$DOMAIN' < /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf

echo 'Env vars replaced, starting nginx...'

exec "$@"
