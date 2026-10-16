using System.Diagnostics;

namespace GenericPadBridge;

public sealed record BridgeTimingSnapshot(int Samples,double ProcessingMeanMs,double ProcessingP95Ms,double ProcessingMaxMs,double IntervalMeanMs,double IntervalP95Ms,double ObservedHz,long ReadErrors,long OutputErrors,long LatePolls,double? LastPollAgeMs,double? LastSuccessAgeMs,double[] RecentIntervals)
{
    public bool HasData=>Samples>0;
}

// Measures the app's polling/processing time, not USB, game or display latency.
public sealed class BridgeTiming
{
    private readonly object gate=new();
    private readonly double[] processing=new double[512],intervals=new double[512];
    private int count,intervalCount,next,intervalNext;
    private long previous,lastPoll,lastSuccess,readErrors,outputErrors,latePolls;
    public void Record(long started,long submitted,long completed,bool inputRead,bool outputExpected,bool outputWritten)
    {
        if(started<=0||completed<started||submitted<started||submitted>completed)return;
        lock(gate)
        {
            if(previous>0&&started>=previous)
            {
                double gap=Stopwatch.GetElapsedTime(previous,started).TotalMilliseconds;
                intervals[intervalNext]=gap;intervalNext=(intervalNext+1)%intervals.Length;intervalCount=Math.Min(intervals.Length,intervalCount+1);if(gap>25)latePolls++;
            }
            previous=lastPoll=started;
            if(!inputRead)readErrors++;else if(outputExpected&&!outputWritten)outputErrors++;
            // A paused or disconnected virtual output has no submitted report to time.
            if(inputRead&&outputExpected&&outputWritten){processing[next]=Stopwatch.GetElapsedTime(started,submitted).TotalMilliseconds;next=(next+1)%processing.Length;count=Math.Min(processing.Length,count+1);lastSuccess=submitted;}
        }
    }
    public BridgeTimingSnapshot Snapshot(long? now=null)
    {
        lock(gate)
        {
            var times=processing.Take(count).Order().ToArray();var gaps=intervals.Take(intervalCount).Order().ToArray();
            double p95(double[] a)=>a.Length==0?0:a[(int)Math.Ceiling(a.Length*.95)-1];
            double gap=gaps.Length==0?0:gaps.Average();int shown=Math.Min(intervalCount,120);var recent=new double[shown];
            for(int i=0;i<shown;i++)recent[i]=intervals[(intervalNext-shown+i+intervals.Length)%intervals.Length];
            long at=now??Stopwatch.GetTimestamp();double? age(long value)=>value==0?null:Math.Max(0,Stopwatch.GetElapsedTime(value,at).TotalMilliseconds);
            return new(count,times.Length==0?0:times.Average(),p95(times),times.Length==0?0:times[^1],gap,p95(gaps),gap>0?1000/gap:0,readErrors,outputErrors,latePolls,age(lastPoll),age(lastSuccess),recent);
        }
    }
    public void Reset(){lock(gate){count=intervalCount=next=intervalNext=0;previous=lastPoll=lastSuccess=readErrors=outputErrors=latePolls=0;}}
}
