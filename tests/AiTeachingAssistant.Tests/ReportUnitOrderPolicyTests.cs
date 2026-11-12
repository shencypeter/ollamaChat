using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class ReportUnitOrderPolicyTests
{
    [Fact]
    public void SameUnitEditDoesNotWriteAnOldOrderBack()
    {
        var unit=Guid.NewGuid();
        Assert.Null(ReportUnitOrderPolicy.OrderToWrite(unit,unit,7));
        Assert.Equal(9,ReportUnitOrderPolicy.OrderToWrite(unit,Guid.NewGuid(),9));
        Assert.Equal(0,ReportUnitOrderPolicy.OrderToWrite(unit,null,0));
    }
}
