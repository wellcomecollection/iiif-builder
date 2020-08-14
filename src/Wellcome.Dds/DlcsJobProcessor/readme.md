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