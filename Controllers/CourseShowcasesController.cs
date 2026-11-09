using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;
[ApiController,Authorize,Route("api/courses/{course:guid}/showcases")]
public sealed class CourseShowcasesController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
 private CourseShowcases Workflow=>new(source);
 private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 private async Task<IActionResult> Run(Func<Task<IActionResult>> work){Response.Headers.CacheControl="no-store";try{return await work();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
 [HttpGet]public Task<IActionResult> List(Guid course,CancellationToken ct)=>Run(async()=>Ok(await Workflow.List(course,Actor,experience.IsPractice,ct)));
 [HttpGet("sources")]public Task<IActionResult> Sources(Guid course,CancellationToken ct)=>Run(async()=>Ok(await Workflow.Sources(course,Actor,experience.IsPractice,ct)));
 [HttpPost]public Task<IActionResult> Create(Guid course,ShowcaseCreateInput input,CancellationToken ct)=>Run(async()=>Ok(new{id=await Workflow.Create(course,Actor,experience.IsPractice,input,ct)}));
 [HttpGet("{id:guid}")]public Task<IActionResult> Get(Guid course,Guid id,CancellationToken ct)=>Run(async()=>Ok(await Workflow.Get(course,id,Actor,experience.IsPractice,false,ct)));
 [HttpPost("{id:guid}/preview")]public Task<IActionResult> Preview(Guid course,Guid id,CancellationToken ct)=>Run(async()=>Ok(await Workflow.Get(course,id,Actor,experience.IsPractice,true,ct)));
 [HttpPut("{id:guid}")]public Task<IActionResult> Save(Guid course,Guid id,ShowcaseContentInput input,CancellationToken ct)=>Run(async()=>{await Workflow.Save(course,id,Actor,experience.IsPractice,input,null,ct);return NoContent();});
 [HttpPost("{id:guid}/attachments"),RequestSizeLimit(22*1024*1024)]public Task<IActionResult> Upload(Guid course,Guid id,IFormFile file,[FromForm]int expectedVersion,CancellationToken ct)=>Run(async()=>{if(file.Length is <1 or >20971520||!Path.GetExtension(file.FileName).Equals(".pdf",StringComparison.OrdinalIgnoreCase))return BadRequest(new{error="請上傳20MB內PDF展示副本。"});using var b=new MemoryStream();await file.CopyToAsync(b,ct);await Workflow.Upload(course,id,Actor,experience.IsPractice,expectedVersion,b.ToArray(),ct);return NoContent();});
 [HttpPost("{id:guid}/publish")]public Task<IActionResult> Publish(Guid course,Guid id,ShowcasePublishInput input,CancellationToken ct)=>Run(async()=>{await Workflow.Publish(course,id,Actor,experience.IsPractice,input,ct);return NoContent();});
 [HttpPost("{id:guid}/withdraw")]public Task<IActionResult> Withdraw(Guid course,Guid id,ShowcaseVersionInput input,CancellationToken ct)=>Run(async()=>{await Workflow.Withdraw(course,id,Actor,experience.IsPractice,input.ExpectedVersion,ct);return NoContent();});
 [HttpGet("{id:guid}/attachment")]public Task<IActionResult> Attachment(Guid course,Guid id,[FromQuery]bool draft,CancellationToken ct)=>Run(async()=>{var b=await Workflow.Attachment(course,id,Actor,experience.IsPractice,draft,ct);Response.Headers["X-Content-Type-Options"]="nosniff";return File(b,"application/octet-stream","showcase.pdf");});
}
