# iiif-builder

IIIF Presentation API Server and related services

## Entry Points

There are 4 main components:

* Wellcome.Dds.Server - API.
* Wellcome.Dds.Dashboard - UI.
* WorkflowProcessor - background service for handling requested workflow jobs.
* DlcsJobProcessor - background service for handling requested dlcs jobs.

## Deploying :rocket:

All services are deployed as Docker containers via Jenkins. There is a Dockerfile- and Jenkinsfile- per entrypoint. 

All environments use the same Jenkinsfile, with parameters controlled in [Jenkins](https://jenkins.dlcs.io/) (not publicly available). All Jenkinsfiles use [pipeline syntax](https://www.jenkins.io/doc/book/pipeline/syntax/) use the same stages:

* "Fetch" - checkout from Git (branch controlled in Jenkins).
* "Image" - `docker build`, tagged `:jenkins`.
* "Tag" - `docker tag` - tagged with commit SHA1 and environment-specific tag.
* "Push" - `docker push` - push changes to ECR.
* "Bounce" - Update ECS service and wait until stable. Will fail if not stable after 10mins.
