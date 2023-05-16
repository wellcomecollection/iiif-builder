FROM postgres:12.5-alpine
COPY init.sql /docker-entrypoint-initdb.d/
