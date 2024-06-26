# iiif-builder

IIIF Presentation API Server and related services.

See [iiif-builder-infrastructure](https://github.com/wellcomecollection/iiif-builder-infrastructure) repo for related infrastructure.

## Projects

There are 4 main projects that serve as deployable artifacts, detailed below.

> All apps explose health-checks on `/management/healthcheck`

### Wellcome.Dds.Server

API. Used for serving IIIF resources (TODO) and provisioning roles to DLCS by authenticating users with Sierra via REST API.

### Wellcome.Dds.Dashboard

UI for monitoring and managing workflow process. Required wellcomecloud account for access.

### WorkflowProcessor

Background service for handling requested workflow jobs from Goobi. 

### DlcsJobProcessor

background service for handling requested dlcs jobs, created by WorkflowProcessor.

## Tech :robot:

* dotnet 6
* EF Core using PostgreSQL
* [AWS SDK](https://github.com/aws/aws-sdk-net/)

Test projects use:
* [xUnit](https://xunit.net/)
* [FluentAssertions](https://fluentassertions.com/) for assertions
* [FakeItEasy](https://fakeiteasy.github.io/) for fakes/mocks

## Deploying :rocket:

All services are deployed as Docker containers via Jenkins. There is a Dockerfile-* per entrypoint and 2 parameterised jenkinsfiles.

All environments use the same Jenkinsfile, with parameters controlled in [Jenkins](https://jenkins.dlcs.io/) (not publicly available). All Jenkinsfiles use [pipeline syntax](https://www.jenkins.io/doc/book/pipeline/syntax/).

### Database Migrations

There are 2 databases per environment, both use EFCore and migrations:

* DDS - `Wellcome.Dds.Repositories.DdsContext`
* DDSInstrumentation - `Wellcome.Dds.AssetDomainRepositories.DdsInstrumentationContext`

Database migrations are automatically applied on start of the Dashboard project for all environments _except Production_. This is controlled in dashboard startup:

```cs
if (!env.IsProduction())
{
    UpdateDatabase(logger);
}
```

For production, the migrations need to be manually applied. Idempotent scripts can be generated by using the following commands (from `./src/Wellcome.Dds/`):

```bash
# generate all migrations for DDS
dotnet ef migrations script -i -o ./dds.sql -p Wellcome.Dds.Repositories -s Wellcome.Dds.Dashboard -c DdsContext

# generate migrations from SampleCurrentMigration for DDS
dotnet ef migrations script SampleCurrentMigration -i -o ./dds.sql -p Wellcome.Dds.Repositories -s Wellcome.Dds.Dashboard -c DdsContext

# generate all migrations for DDS Instrumentation
dotnet ef migrations script -i -o ./dds_instr.sql -p Wellcome.Dds.AssetDomainRepositories -s Wellcome.Dds.Dashboard -c DdsInstrumentationContext

# generate migrations from SampleCurrentMigration for DDS Instrumentation
dotnet ef migrations script SampleCurrentMigration -i -o ./dds_instr.sql -p Wellcome.Dds.AssetDomainRepositories -s Wellcome.Dds.Dashboard -c DdsInstrumentationContext
```
> See [msdn](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#sql-scripts) for more details on possible options.

Connection strings for the production databases can be found in AWS Secrets Manager: `iiif-builder/production/dds-connstr` and `iiif-builder/production/ddsinstrumentation-connstr`.

Use port forwarding via the AWS bastion to access RDS instance, and the above connection-strings for credentials.

> Run any migrations as the application user, or remember to grant privs to any new items

## Running

The [`dotnet cli`](https://docs.microsoft.com/en-us/dotnet/core/tools/) can be used to run the apps outside of an IDE.

> All of the following samples commands assume a starting folder of `/src/Wellcome.Dds`.

### Apps

Applications can be run using the following commands.

```bash
# API http://localhost:8084
dotnet run -p ./Wellcome.Dds.Server/Wellcome.Dds.Server.csproj

# Dashboard http://localhost:8085/dash
dotnet run -p ./Wellcome.Dds.Dashboard/Wellcome.Dds.Dashboard.csproj

# WorkflowProcessor http://localhost:8081 (http for healthchecks only)
dotnet run -p ./WorkflowProcessor/WorkflowProcessor.csproj

# DlcsJobProcessor http://localhost:8082 (http for healthchecks only)
dotnet run --project ./DlcsJobProcessor/DlcsJobProcessor.csproj
```

> To run applications some secrets will need to be added to relevant `appsettings.Development.json` (e.g. db-connection strings and API keys).

### Tests

Tests can be run using the following commands:

```bash
# Run ALL tests in solution
dotnet test

# Run all tests, except those with "Database" category.
# These can be slow as they run a postgres Docker container and apply migrations
dotnet test --filter Category\!=Database

# Run test for specific project
dotnet test ./Utils.Tests/Utils.Tests.csproj
```

The following categories are used for tests:

* `Integration` - this doesn't signify much but marks tests as being integration tests (generally e-e tests using [WebApplicationFactory](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1?view=aspnetcore-3.0))
* `Database` - these tests require an external database, currently handled by `Wellcome.Dds.Server.Tests.Integration.DatabaseFixture`
* `Manual` - for manual verification with multiple external dependencies (e.g. AWS creds, s3 objects) and not expected to be routinely ran.

Unit tests are ran as part of building the Docker image. `Database` and `Manual` categories are ignored when running in the Dockerfile (`RUN dotnet test "Wellcome.Dds.sln" --filter "Category!=Database&Category!=Manual"`). _Depending on requirements it may make sense to ignore these in IDE for standard test runs_.

## AWS Profiles

For local development there are 3 different AWS profiles that are required. These are setup in the appsettings as a configSection with profile + region properties, e.g.:

```json
"Storage-AWS": {
  "Profile": "wcstorage",
  "Region": "eu-west-1"
},
"Dds-AWS": {
  "Profile": "wcdev",
  "Region": "eu-west-1"
},
"Platform-AWS": {
  "Profile": "wcplatform",
  "Region": "eu-west-1"
},
```

They are:

* `Storage-AWS` - contains name of profile that can access Wellcome's internal storage buckets for reading METS files etc.
* `Dds-AWS` - profile that can access iiif-builder buckets for presentation, annotations and text.
* `Platform-AWS` - profile that has rights to publish to SNS topic for CloudFront invalidation.

These configSection names are used locally only, e.g. `services.AddDefaultAWSOptions(Configuration.GetAWSOptions("Dds-AWS"));`

As the specified AWS options configSection name is not present in deployed config, the SDK will fallback to the default credential provider chain and use ECS container credentials.

### Named AWS Clients

`Storage-AWS` and `Dds-AWS` profiles are used to access S3. To help with this the `NamedAmazonS3ClientFactory` is used, with an extension method to help configure it:

```cs
// register in INamedAmazonS3ClientFactory singleton with into IServiceCollection
services.AddNamedS3Clients(Configuration, NamedClient.All);

// get required IAmazonS3 client
IAmazonS3 ddsClient = factory.Get(NamedClient.Dds);
```