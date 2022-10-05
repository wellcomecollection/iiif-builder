using System;

namespace Wellcome.Dds.Common;

/// <summary>
/// Message that will be sent by both Goobi and Archivematica, to be picked up from queue for processing
/// </summary>
public class WorkflowMessage
{
    public string Identifier { get; set; }
    public string Space { get; set; }
    public string Origin { get; set; }
    public DateTime TimeSent { get; set; }

    public override string ToString()
    {
        return $"{Identifier} ({Space}) from {Origin}";
    }
}