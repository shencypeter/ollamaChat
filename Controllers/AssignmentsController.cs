using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AiTeachingAssistant.Controllers;

[ApiController,Route("api"),Authorize]
public sealed class AssignmentsController(AssignmentWorkflow workflow):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run<T>(Func<Task<T>> operation)
    {
        try{return Ok(await operation());}
        catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}
    }
    [HttpGet("courses/{courseId:guid}/assignments")]
    public Task<IActionResult> List(Guid courseId,CancellationToken ct)=>Run(()=>workflow.List(courseId,Actor,ct));
    [AiTeachingAssistant.Services.CourseCapability("assessments")]
    [HttpPost("courses/{courseId:guid}/assignments")]
    public Task<IActionResult> Create(Guid courseId,SaveAssignmentRequest request,CancellationToken ct)=>Run(()=>workflow.Create(courseId,Actor,request,ct));
    [HttpGet("assignments/{id:guid}")]
    public Task<IActionResult> Get(Guid id,CancellationToken ct)=>Run(()=>workflow.Get(id,Actor,ct));
    [AiTeachingAssistant.Services.CourseCapability("assessments")]
    [HttpPut("assignments/{id:guid}")]
    public Task<IActionResult> Update(Guid id,SaveAssignmentRequest request,CancellationToken ct)=>Run(()=>workflow.Update(id,Actor,request,ct));
    [AiTeachingAssistant.Services.CourseCapability("assessments")]
    [HttpPost("assignments/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id,CancellationToken ct)=>Run(()=>workflow.Transition(id,Actor,"published",ct));
    [AiTeachingAssistant.Services.CourseCapability("assessments")]
    [HttpPost("assignments/{id:guid}/close")]
    public Task<IActionResult> Close(Guid id,CancellationToken ct)=>Run(()=>workflow.Transition(id,Actor,"closed",ct));
    [HttpGet("assignments/{id:guid}/my-submission")]
    public Task<IActionResult> Own(Guid id,CancellationToken ct)=>Run(()=>workflow.Own(id,Actor,ct));
    [HttpPost("assignments/{id:guid}/my-submission"),RequestSizeLimit(2_000_000)]
    public Task<IActionResult> Save(Guid id,SaveSubmissionRequest request,CancellationToken ct)=>Run(()=>workflow.Save(id,Actor,request,ct));
    [HttpGet("assignments/{id:guid}/class-roster")]
    public Task<IActionResult> Roster(Guid id,CancellationToken ct)=>Run(()=>workflow.ClassRoster(id,Actor,ct));
    [HttpGet("assignments/{id:guid}/submissions")]
    public Task<IActionResult> Submitted(Guid id,CancellationToken ct)=>Run(()=>workflow.Submitted(id,Actor,ct));
    [HttpGet("assignments/{id:guid}/submissions/{submissionId:guid}")]
    public Task<IActionResult> Review(Guid id,Guid submissionId,CancellationToken ct)=>Run(()=>workflow.Review(id,submissionId,Actor,ct));
    [HttpGet("assignments/{id:guid}/submissions/{submissionId:guid}/revisions")]
    public Task<IActionResult> Snapshots(Guid id,Guid submissionId,CancellationToken ct)=>Run(()=>workflow.Snapshots(id,submissionId,Actor,ct));
    [HttpGet("courses/{courseId:guid}/rubrics")]
    public Task<IActionResult> Rubrics(Guid courseId,CancellationToken ct)=>Run(()=>workflow.Rubrics(courseId,Actor,ct));
    [HttpPost("courses/{courseId:guid}/rubrics")]
    public Task<IActionResult> CreateRubric(Guid courseId,SaveRubricRequest request,CancellationToken ct)=>Run(()=>workflow.SaveRubric(courseId,null,Actor,request,ct));
    [HttpPut("courses/{courseId:guid}/rubrics/{rubricId:guid}")]
    public Task<IActionResult> EditRubric(Guid courseId,Guid rubricId,SaveRubricRequest request,CancellationToken ct)=>Run(()=>workflow.SaveRubric(courseId,rubricId,Actor,request,ct));
    [HttpGet("assignments/{id:guid}/rubric")]
    public Task<IActionResult> Rubric(Guid id,CancellationToken ct)=>Run(()=>workflow.AssignmentRubric(id,Actor,ct));
    [HttpGet("assignments/{id:guid}/revisions/{revisionId:guid}/quiz-grading")]
    public Task<IActionResult> QuizResult(Guid id,Guid revisionId,CancellationToken ct)=>Run(()=>workflow.QuizResult(id,revisionId,Actor,ct));
    [HttpGet("assignments/{id:guid}/revisions/{revisionId:guid}/grades")]
    public Task<IActionResult> Grades(Guid id,Guid revisionId,CancellationToken ct)=>Run(()=>workflow.Grades(id,revisionId,Actor,ct));
    [AiTeachingAssistant.Services.CourseCapability("grading")]
    [HttpPost("assignments/{id:guid}/revisions/{revisionId:guid}/grades")]
    public Task<IActionResult> Grade(Guid id,Guid revisionId,SaveGradeRequest request,CancellationToken ct)=>Run(()=>workflow.SaveGrade(id,revisionId,Actor,request,ct));
    [AiTeachingAssistant.Services.CourseCapability("grading")]
    [HttpPost("assignments/{id:guid}/revisions/{revisionId:guid}/grades/{gradeId:guid}/release")]
    public Task<IActionResult> Release(Guid id,Guid revisionId,Guid gradeId,CancellationToken ct)=>Run(()=>workflow.ReleaseGrade(id,revisionId,gradeId,Actor,ct));
}
