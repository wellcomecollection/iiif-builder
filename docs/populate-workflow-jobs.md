# Populate Workflow Jobs

## Bulk Populate from AWS

The workflow-processor app can be used for bulk population of the workflow-jobs table. See it's readme for details on the various command line arguments.

This section details configuring an EC2 instance for bulk processing.

### Setup EC2

First step is to setup an EC2 instance. Use same setup as current iiif-builder-bastion (same NSG, VPC etc), with the exceptions of:

* Instance type: Use something non-burstable with decent compute power (e.g. `m4-large`) 
* Storage: use root volume with adequate disk space as it needs to download and extract the contents of Wellcome catalogue backup (50GB is more than adequate).
* AMI: Below command from Ubuntu 18.04 (exact details: `aws ec2 describe-images --image-ids ami-06fd78dc2f0b69910 --region eu-west-1`)

### Setup

Once EC2 is running use the following commands to execute:

```bash
# update packages
sudo apt-get update

# clone repo
git clone https://github.com/wellcomecollection/iiif-builder.git
cd iiif-builder/

# install dotnet, from https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap
sudo snap install dotnet-sdk --classic --channel=5.0
sudo snap alias dotnet-sdk.dotnet dotnet
sudo snap install dotnet-runtime-50 --classic
# sudo snap alias dotnet-runtime-50.dotnet dotnet

# restore nuget packages
cd Wellcome.Dds/WorkflowProcessor
dotnet restore

# update appsettings.Production.json
# see iiif-builder-infrastructure for any secrets that need added
sudo vim appsettings.Production.json

# set as 'Production' environment
ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_ENVIRONMENT

# run wf processor, need --no-launch-profile as this sets "Development" env
dotnet run --no-launch-profile --populate-slice 11
dotnet run --no-launch-profile --mopupcd
```

`--populate-slice 11` processed 25,000 jobs and took around 70mins to execute.