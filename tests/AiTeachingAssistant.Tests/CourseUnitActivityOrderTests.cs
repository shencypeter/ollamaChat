using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class CourseUnitActivityOrderTests
{
    [Fact]
    public void NormalizeRejectsUnknownKindsAndDuplicateActivities()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => CourseUnitActivityOrder.Normalize([new("exam", id)]));
        Assert.Throws<ArgumentException>(() => CourseUnitActivityOrder.Normalize([new("material", id), new("material", id)]));
    }

    [Fact]
    public void NormalizePreservesACompleteCrossTypeOrder()
    {
        var material = Guid.NewGuid();
        var quiz = Guid.NewGuid();
        var report = Guid.NewGuid();

        var result = CourseUnitActivityOrder.Normalize([
            new("material", material),
            new("quiz", quiz),
            new("report", report)
        ]);

        Assert.Equal(["material", "quiz", "report"], result.Select(item => item.Kind));
        Assert.Equal([material, quiz, report], result.Select(item => item.Id));
    }
}
