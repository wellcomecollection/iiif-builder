namespace WorkflowProcessor
{
    public class CacheInvalidationOptions
    {
        /// <summary>
        /// ARN for SNS topic to send invalidation requests for iiif.wc.org 
        /// </summary>
        public string InvalidateIIIFTopicArn { get; set; }
        
        /// <summary>
        /// ARN for SNS topic to send invalidation requests for api.wc.org 
        /// </summary>
        public string InvalidateApiTopicArn { get; set; }
    }
}