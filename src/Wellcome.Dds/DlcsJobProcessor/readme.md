# Job Processor

## Controlling Operations

There are a few different configuration options that can be used to control the operation of the job processor. These are stored as standard appSettings:

```json
"JobProcessor": {
    "YieldTimeSecs": 60,
    "Filter": "",
    "Mode": "processqueue"
  },
```

| Setting Name  | Description                                                                           |
|---------------|---------------------------------------------------------------------------------------|
| YieldTimeSecs | How long to wait between runs of the job.                                             |
| Mode          | The type of job process operation to run. Options: `processqueue` and `updatestatus`. |
| Filter        | Optional filter to use for `processqueue` operations. Filters on manifest identifier. |

### Controlling Arguments

These arguments can be controlled as per normal appSettings, including passing on the CLI:

```bash
# run processqueue (default) job with filter of b12345678 
dotnet run DlcsJobProcessor JobProcessor:Filter=b12345678

# run updatestatus job, with yield time of 15s
dotnet run DlcsJobProcessor JobProcessor:Mode=updatestatus JobProcessor:YieldTimeSecs=15
```

## Go File

> This functionality will be updated in the future to be more gracefully handled.

When starting up the JobProcessor will make a check to verify that a known file exists. If this file does not exist, or it is empty then the job processor _won't start_. The file that is checked is `Dds:GoFile` within the `Dds:StatusContainer` bucket.

If required create a go file with a timestamp in it, e.g.:

```bash
# create file
echo '2020-09-25T08:54:08' | dds.txt

# upload to s3
aws s3 cp dds.txt s3://wellcomecollection-stage-iiif-presentation/_status/
```