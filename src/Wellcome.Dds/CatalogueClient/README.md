# Catalogue Client

This is a very bare-bones tool to test a .NET Core client library for the Wellcome Collection Catalogue API.

## Usage

In the CatalogueClient project directory:

```bash
# Prints out catalogue information for the identifier b28047345
dotnet run --id b28047345

# Prints out catalogue information for all the identifiers listed in the file
dotnet run --file examples.txt
```

This baseline app can be extended for utilties that need to process batches of b numbers (or other catalogue identifiers).