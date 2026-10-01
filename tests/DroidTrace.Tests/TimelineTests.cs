using Xunit;
using DroidTrace.Services;

namespace DroidTrace.Tests;

public class TimelineTests
{
    [Fact]
    public void Timeline_Parses_Sms_Date()
    {
        var dir = Path.Combine(Path.GetTempPath(), "droidtrace-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"sms.txt"), "Row: 0 _id=1 address=123 body=hello date=1720000000000 type=1\n");
        var result = TimelineService.Build(dir);
        Assert.Single(result); Assert.Equal("SMS", result[0].Artifact);
        Directory.Delete(dir,true);
    }

    [Fact]
    public void Timeline_Filter_By_Keyword()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new List<DroidTrace.Models.TimelineEvent>
        {
            new(now, "SMS", "_id=1 address=+15551234 body=SecretMeeting date=1720000000000", "content://sms"),
            new(now.AddMinutes(-1), "CALL", "_id=2 number=+19998888 name=Unknown duration=45", "content://call_log/calls"),
            new(now.AddMinutes(-2), "SMS", "_id=3 address=+15551234 body=Confirmed arrival date=1720000060000", "content://sms")
        };

        var filtered = TimelineService.Filter(events, "SecretMeeting");
        Assert.Single(filtered);
        Assert.Contains("SecretMeeting", filtered[0].Summary);
    }

    [Fact]
    public void Timeline_Filter_By_ArtifactType()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new List<DroidTrace.Models.TimelineEvent>
        {
            new(now, "SMS", "sms 1", "content://sms"),
            new(now.AddMinutes(-1), "CALL", "call 1", "content://call_log/calls"),
            new(now.AddMinutes(-2), "SMS", "sms 2", "content://sms")
        };

        var callOnly = TimelineService.Filter(events, null, "CALL");
        Assert.Single(callOnly);
        Assert.Equal("CALL", callOnly[0].Artifact);

        var smsOnly = TimelineService.Filter(events, null, "SMS");
        Assert.Equal(2, smsOnly.Count);
    }
}
