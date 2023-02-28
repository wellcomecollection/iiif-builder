using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Utils.Aws.SQS;

/// <summary>
/// Generic representation of message pulled from queue.
/// Adapted from protagonist for NewtonSoft.JSON
/// </summary>
public class QueueMessage
{
    /// <summary>
    /// The full message body property
    /// </summary>
    public JObject Body { get; set; }

    /// <summary>
    /// Any attributes associated with message
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; }
        
    /// <summary>
    /// Unique identifier for message
    /// </summary>
    public string MessageId { get; set; }
    
    /// <summary>
    /// The name of the queue that this message was from
    /// </summary>
    public string QueueName { get; set; }
}

public static class QueueMessageX
{
    /// <summary>
    /// Get a <see cref="JsonObject"/> representing the contents of the message as raised by source system. This helps
    /// when the message can be from SNS->SQS, SNS->SQS with RawDelivery or SQS directly.
    /// 
    /// If originating from SNS and Raw Message Delivery is disabled (default) then the <see cref="QueueMessage"/>
    /// object will have additional fields about topic etc, and the message will be embedded in a "Message" property.
    /// e.g. { "Type": "Notification", "MessageId": "1234", "Message": { \"key\": \"value\" } }
    /// 
    /// If originating from SQS, or from SNS with Raw Message Delivery enabled, the <see cref="QueueMessage"/> Body
    /// property will contain the full message only.
    /// e.g. { "key": "value" }
    /// </summary>
    /// <remarks>See https://docs.aws.amazon.com/sns/latest/dg/sns-large-payload-raw-message-delivery.html </remarks>
    public static JObject? GetMessageContents(this QueueMessage queueMessage)
    {
        const string messageKey = "Message";
        if (queueMessage.Body.ContainsKey("TopicArn") && queueMessage.Body.ContainsKey(messageKey))
        {
            // From SNS without Raw Message Delivery
            try
            {
                var value = queueMessage.Body[messageKey]!.Value<string>();
                return JObject.Parse(value);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // From SQS or SNS with Raw Message Delivery
        return queueMessage.Body;
    }
} 