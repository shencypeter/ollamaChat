using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class ResourceMetadataPolicyTests
{
 [Theory][InlineData("2026-11-09")][InlineData("2028-02-29")]
 public void TeachingDatesStayCalendarDates(string value)=>Assert.Equal(value,ResourceMetadataPolicy.Date(value)!.Value.ToString("yyyy-MM-dd"));
 [Theory][InlineData("2026-02-29")][InlineData("2026-11-31")][InlineData("11/09/2026")][InlineData("2026-1-1")][InlineData("2026-11-09T00:00:00Z")]
 public void RejectsInvalidOrTimezoneBearingDates(string value)=>Assert.Throws<ArgumentException>(()=>ResourceMetadataPolicy.Date(value));
 [Fact]public void EmptyDateAndSummaryCanBeCleared(){Assert.Null(ResourceMetadataPolicy.Date(null));Assert.Equal("",ResourceMetadataPolicy.Summary("  "));Assert.Equal("<b>text</b>",ResourceMetadataPolicy.Summary(" <b>text</b> "));Assert.Throws<ArgumentException>(()=>ResourceMetadataPolicy.Summary(new string('x',10001)));}
 [Theory][InlineData("teaching_desc")][InlineData("teaching_asc")][InlineData("uploaded_desc")]
 public void SortUsesFixedSql(string order)=>Assert.DoesNotContain(";",ResourceMetadataPolicy.Order(order));
 [Fact]public void RejectsSqlInSort()=>Assert.Throws<ArgumentException>(()=>ResourceMetadataPolicy.Order("created_at;DROP TABLE courses"));
}
