## Workflow queues and topics - questions/decisions

There are two existing SNS topics owned by _*storage*_:

- born-digital-bag-notifications-prod
- born-digital-bag-notifications-staging

We think there will be two new SNS topics for Goobi:

- digitised-bag-notifications-prod
- digitised-bag-notifications-staging

_(or similar names: "born-digital" and "digitised" match the current storage prefixes)_

There are/will be queues subscribed to these topics. Where do the queues live? We have currently one production queue, known as:

* arn:aws:sqs:eu-west-1:975596993436:born-digital-notifications-prod (in storage acct)
* arn:aws:sqs:eu-west-1:653428163053:born-digital-notifications-prod (in digirati acct)

There is also an equivalent queue for staging storage. 

**Conclusion - the queues should live in the Digirati account, as subscribers to external (storage or platform) information.**

The WorkflowProcessor listens to just the original born-digital-bag-notifications-(env) queue at the moment, because we've only tested the one workflow and it's only for born-digital events.

Will we have another pair of queues for prod and staging that recieve messages from the digitised-bag-notifications-xxx topic? So that a running WorkflowProcessor is picking up from two queues?
Or should we have one queue that subscribes to both born-digital and digitised topics, and gets workflow messages from both? (we should rename that existing queue if that's the case).

An argument for keeping them separate is ease of pausing workflow from one source (without having to still pick up messages to see what the source is).

**Conclusion - having two queues is simpler and more flexible.**

Either way the dashboard will need to pick the right topic to notify when manually running an end-to-end workflow from the dashboard, pretending to be Goobi or the storage service (it puts "dashboard" in the `origin` of the [message](https://github.com/wellcomecollection/iiif-builder/blob/develop/src/Wellcome.Dds/Wellcome.Dds.Common/WorkflowMessage.cs) to be clear where the message came from).

We can have more than one WorkflowProcessor service picking up a job, but we don't want more than one environment synchronising with the same DLCS space.
So DDS production and test each have their own queue(s) subscribed to both Goobi and born-digital topics; _prod_ writes to space 5 and _test_ writes to space 7 (this is NEW, previously _test_ wrote to space 6).

DDS staging writes to DLCS space 6, as now.

In normal operation the `Test` environment does not listen to queues, or it throws away messages, etc - we control this through a flag so we can turn test on went we want to observe it. Even when test is running, its workflowProcessor is not always acting on the messages it picks up; it needs a control switch to act on the messages. We don't want or need the test environment to be synching production storage to DLCS).

**Conclusion - Something that hooks into `IOptions` model/appSettings is an idiomatic control switch for a modern .NET service. Maybe something like parameterStore that could periodically refresh `IOptionsMonitor`.**

Stage workflow should listen to Goobi stage and Storage stage, and sync with DLCS, all the time it's running (assuming much less traffic comes through stage, and the traffic that does come through still needs to be looked at via IIIF/DLCS).

Dev environments, whether local or ad-hoc deployed, can use a separate set of queues just for dev, opting in to act on the messages. Or they could use the Test queues.

So:

SNS Topics

- born-digital-bag-notifications-prod*
- digitised-bag-notifications-prod
- born-digital-bag-notifications-staging*
- digitised-bag-notifications-staging

SQS Queues

- born-digital-bag-notifications-prod*
- digitised-bag-notifications-prod
- born-digital-bag-notifications-staging*
- digitised-bag-notifications-staging
- born-digital-bag-notifications-test (subscribes to topic born-digital-bag-notifications-prod)
- digitised-bag-notifications-test (subscribes to topic digitised-bag-notifications-prod)
- born-digital-bag-notifications-dev
- digitised-bag-notifications-dev

*existing

These last 2 subscribe to the staging storage topics by default but can be switched to subscribe to the prod storage topics if required. Or, if we don't mind a profusion of SQS, we could replace the last two by:

- born-digital-bag-notifications-dev-prod-storage
- digitised-bag-notifications-dev-prod-storage
- born-digital-bag-notifications-dev-stage-storage
- digitised-bag-notifications-dev-stage-storage

... to give us independent queues that won't be getting picked up by any deployed WorkflowProcessor, for all combinations we'd want to debug / investigate.
This requires 4 queues being kept up that are hardly ever used, though. Maybe **not** have these but just have the dev ones as in the previous list but have two extra _topics_:

- born-digital-bag-notifications-dev
- digitised-bag-notifications-dev

Neither Goobi nor the storage service broadcast to these topics, we have to manually send to them, for whatever dev setup we are testing to pick up.

Conclusion - have two dev topics that can be used for ad hoc broadcasting. Unlike the topics associated with the storage service and with Goobi, these two dev topics can live in the Digirati account.