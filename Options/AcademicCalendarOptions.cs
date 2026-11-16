using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Options;

public sealed class AcademicCalendarOptions : IValidatableObject
{
    public const string SectionName = "AcademicCalendar";
    [RegularExpression(@"^[1-9][0-9]{0,2}-(1|2|S)$", ErrorMessage = "CurrentTerm must use academic year-semester, for example 115-1, 115-2 or 115-S.")]
    public string? CurrentTerm { get; set; }

    [Range(1,12)] public int FirstSemesterStartMonth { get; set; } = 8;
    [Range(1,12)] public int SecondSemesterStartMonth { get; set; } = 2;

    public string ResolveCurrentTerm(DateTimeOffset utcNow)
    {
        if(!string.IsNullOrWhiteSpace(CurrentTerm))return CurrentTerm;
        var local=utcNow.ToOffset(TimeSpan.FromHours(8));
        var academicYear=local.Year-1911-(local.Month<FirstSemesterStartMonth?1:0);
        var semester=local.Month>=FirstSemesterStartMonth||local.Month<SecondSemesterStartMonth?"1":"2";
        return academicYear.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+semester;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if(SecondSemesterStartMonth>=FirstSemesterStartMonth)
            yield return new ValidationResult("SecondSemesterStartMonth must precede FirstSemesterStartMonth.",[nameof(SecondSemesterStartMonth),nameof(FirstSemesterStartMonth)]);
    }
}
