namespace AiTeachingAssistant.Services;

// Additional restrictions on an active TA membership. Course-role checks still apply.
[AttributeUsage(AttributeTargets.Method)]
public sealed class CourseCapabilityAttribute(string capability):Attribute
{
    public string Capability {get;}=capability;
}
