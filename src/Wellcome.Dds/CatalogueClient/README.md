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

This baseline app can be extended for utilities that need to process batches of b numbers (or other catalogue identifiers).

```bash
# Downloads the daily dump file to the system's temp directory, and unpacks it.
dotnet run --update

# runs the "display-bnumber" operation - this finds and displays all digitised b numbers.
# The skip parameter is used for taking slices through the data. 
# e.g., --skip 100 means that only every 100th line of the dump file is processed.
# the default is 1, every line is processed.
dotnet run --bulkop display-bnumber --skip 100
```