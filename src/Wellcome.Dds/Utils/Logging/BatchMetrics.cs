using System.Diagnostics;

namespace Utils.Logging;

public class BatchMetrics
{
    public int BatchCounter { get; set; }
    public long LastBatchTime { get; set; }
    public long TotalTime { get; set; }
    public long MinBatchTime { get; set; }
    public int MinBatchCount { get; set; }
    public long MaxBatchTime { get; set; }
    public int MaxBatchCount { get; set; }
    
    public int BatchSize { get; set; }
    
    public long AverageBatchTime => TotalTime / BatchCounter;

    public string Summary => $"Total batches: {BatchCounter}; " +
                             $"Avg batch time {AverageBatchTime}; " +
                             $"Min batch time {MinBatchTime} ({MinBatchCount} items); " +
                             $"Max Batch time {MaxBatchTime} ({MaxBatchCount} items)";

    private readonly Stopwatch stopwatch;

    public BatchMetrics()
    {
        stopwatch = new Stopwatch();
    }

    public void BeginBatch(int batchSize = -1)
    {
        stopwatch.Restart();
        BatchCounter++;
        BatchSize = batchSize;
    }

    public void EndBatch(int batchSize = -1)
    {
        if (batchSize > 0)
        {
            // you might not know BatchSize until you end the batch
            BatchSize = batchSize;
        }
        LastBatchTime = stopwatch.ElapsedMilliseconds;
        if (BatchCounter == 1)
        {
            MinBatchTime = LastBatchTime;
            MinBatchCount = BatchSize;
            MaxBatchTime = LastBatchTime;
            MaxBatchCount = BatchSize;
        }
        else
        {
            if (LastBatchTime < MinBatchTime)
            {
                MinBatchTime = LastBatchTime;
                MinBatchCount = BatchSize;
            }
            else if (LastBatchTime >= MaxBatchTime)
            {
                MaxBatchTime = LastBatchTime;
                MaxBatchCount = BatchSize;
            }
        }
        TotalTime += LastBatchTime;
    }
}