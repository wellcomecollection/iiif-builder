using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain;

public class DlcsCallContext
{
    public DlcsCallContext(string label, string identifier)
    {
        Id = Guid.NewGuid();
        Label = label;
        Identifier = identifier;
    }
    
    public DlcsCallContext(string label, int jobId, string identifier)
    {
        Id = Guid.NewGuid();
        Label = label;
        JobId = jobId;
        Identifier = identifier;
    }

    public void AddCall(string httpMethod, string uri, Guid correlationId)
    {
        DlcsCalls.Add(new CorrelatedDlcsCall(httpMethod, uri, correlationId));
    }
    
    public Guid Id { get; }
    public string Identifier { get; set; }
    public string Label { get; }
    public int? JobId { get; }
    public Guid? SyncOperationId { get; set; }

    public List<CorrelatedDlcsCall> DlcsCalls = new();

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}

public class CorrelatedDlcsCall
{
    public CorrelatedDlcsCall(string httpMethod, string uri, Guid correlationId)
    {
        HttpMethod = httpMethod;
        Uri = uri;
        CorrelationId = correlationId;
    }

    public Guid CorrelationId { get; set; }
    public string HttpMethod { get; set; }
    public string Uri { get; set; }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}