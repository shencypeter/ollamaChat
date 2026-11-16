using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class ChatPerformanceMetricsTests
{
    [Fact]
    public void OnlyValidNumericMeasurementsAndKnownCompletionCodesAreReturned()
    {
        var result=ChatPerformanceMetrics.Clean(new RequestMetrics(-1,-2,-3,-4,-5,-6,-7,double.NaN,-8,"untrusted upstream text"));
        Assert.Equal(new RequestMetrics(0,0,0,0,0,0,0,0,0,"unknown"),result);
    }
}
