## Workflow queues and topics - questions/decisions

There are two existing SNS topics owned by _*storage*_:

- born-digital-bag-notifications-prod
- born-digital-bag-notifications-staging

And there are two new SNS topics for Goobi, owned by _*workflow*_

- digitised-bag-notifications-workflow-prod
- digitised-bag-notifications-workflow-staging

At the time these first two topics were created, queues were also created, subscribing to these topics. We don't use these queues:

* arn:aws:sqs:eu-west-1:975596993436:born-digital-notifications-prod (in storage acct)
* arn:aws:sqs:eu-west-1:653428163053:born-digital-notifications-prod (in digirati acct)

There is also an equivalent queue for staging storage. 

## DDS Approach

In our terraform, we reference these four topics as data, but we creates our own queues for each environment:

 - Two for each of our prod, stage, test (aka stage-prod) and also local dev environments. 
 
 We then subscribe some of these queues to the storage (born-digital) or workflow (goobi) topics.

This means the running DDS has no knowledge of the topics, and certainly never publishes to them. For the scenario of a manual initiation of a workflow from the dashboard, the dashboard sends a message on its appropriate, DDS-infra queue.

The WorkflowProcessor listens to a list of queues, set in DdsOptions `WorkflowMessageListenQueues` (so we can add others for testing), and the dashboard is capable of sending messages to one of two specific queues for its environment, one for born digital and one for digitised, the settings `DashboardPushDigitisedQueue` and `DashboardPushBornDigitalQueue`. In practice, the workflowProcessor in that environment is listening to these same two queues, but it can listen to others.

This means that DDS terraform manages all the notification queues it uses, the only point of contact with storage/workflow infrastructure is the queue subscription in terraform.

We can have more than one WorkflowProcessor service picking up a job, but we don't want more than one environment synchronising with the same DLCS space.
So DDS production and test each have their own queue(s) subscribed to both Goobi and born-digital topics.

 - DDS production writes to space 5, as now.
 - DDS staging writes to DLCS space 6, as now.
 - DDS test (stage-prod) writes to space 7 (this is NEW)

In normal operation the `Test` environment WorkflowProcessor does not listen to queues. This is set via `WorkflowMessagePoll` in DdsOptions. It might be that Test WorkflowProcessor is deployed with an empty list for `WorkflowMessageListenQueues`.

Stage should listen to Goobi stage and Storage stage, and sync with DLCS, all the time it's running (assuming much less traffic comes through stage, and the traffic that does come through still needs to be looked at via IIIF/DLCS).