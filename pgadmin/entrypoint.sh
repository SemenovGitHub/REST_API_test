#!/bin/sh
set -eu
cp /pgpassfile /var/lib/pgadmin/pgpass
chmod 600 /var/lib/pgadmin/pgpass
chown 5050:5050 /var/lib/pgadmin/pgpass
exec /entrypoint.sh
