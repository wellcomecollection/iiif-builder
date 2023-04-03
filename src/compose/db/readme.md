# DB

A custom Dockerfile for local development. It is a basic postgreSQL 12.5 instance that will copy and run contents of `init.sql` on startup.

`init.sql` creates 2 databases:

* `dds` with user `dds_user:dds_password`
* `dds_instrumentation` with user `dds_instrumentation_user:dds_instrumentation_password`

> This is only intended for local development.