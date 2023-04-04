# Docker Compose

Compose file to ease local development, starts postgres instance with 2 databases for dds + dds_instrumentation on port 5442. 

Running the dashboard project against this will run EF migrations.

Run with:

```bash
docker compose -f docker-compose.yml up
```

Use connection strings:

```json
{
"ConnectionStrings": {
    "Dds": "Server=127.0.0.1;Port=5442;Database=dds;User Id=dds_user;Password=dds_password;",
    "DdsInstrumentation": "Server=127.0.0.1;Port=5442;Database=dds_instrumentation;User Id=dds_instrumentation_user;Password=dds_instrumentation_password;"
  },
}
```