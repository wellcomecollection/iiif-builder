### Usage

(no args)

Process workflow jobs from the table - standard continuous behaviour of this service.
TODO - more detail on this!

`--finish-all`

Mark all non-taken jobs as finished (reset them)

`--populate-file {filepath}`

Create workflow jobs from the b numbers in a file

`--populate-slice {skip}`

Create workflow jobs from a subset of all possible digitised b numbers.
This will download and unpack the catalogue dump file, take every {skip} lines,
produce a list of unique b numbers that have digital locations, then register jobs for them.
e.g., skip 100 will populate 1% of the total possible jobs, skip 10 will populate 10%, skip 1 will do ALL jobs.

`--workflow-options {flags-int}`

Optional argument for the two populate-*** operations.
This will create a job with a set of processing options that will override the default RunnerOptions, when
the job is picked up by the WorkflowProcessor.
This flags integer can be obtained by creating a new RunnerOptions instance and calling ToInt32().
There is also a helper RunnerOptions.AllButDlcsSync() call for large-scale operations.

`--workflow-options 30`

This is (currently) the all-but-DLCS flags value.

`--offset {offset}`

Create workflow jobs from a subset of all possible digitised b numbers.
This will produce a list of b numbers that have digital locations, ignoring the first {offset} entries that
have digitised b numbers (NOT skipping first X lines).
Used alongside `--populate-slice`

`--catalogue-dump {path}`

Specify the catalogue dump file to use, this will NOT download a fresh copy of catalogue.
If specified file is not found, program will throw an exception.
Used alongside `--populate-slice`