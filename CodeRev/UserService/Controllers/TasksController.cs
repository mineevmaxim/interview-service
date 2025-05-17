﻿using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using UserService.DAL.Models.Enums;
using UserService.Helpers.Tasks;
using UserService.Models.Tasks;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [EnableCors]
    [ApiController]
    public class TasksController : Controller
    {
        private const string Solution = "solution";

        private readonly ITaskHelper taskHelper;
        private readonly ITaskCreator taskCreator;
        private readonly ITaskHandler taskHandler;

        public TasksController(ITaskHelper taskHelper, ITaskCreator taskCreator, ITaskHandler taskHandler)
        {
            this.taskHelper = taskHelper;
            this.taskCreator = taskCreator;
            this.taskHandler = taskHandler;
        }

        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpGet]
        public IActionResult GetTasks()
        {
            return Ok(taskHelper.GetAllTasks());
        }

	[Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpDelete]
        public IActionResult DeleteTaskById([Required] [FromQuery(Name = "id")] Guid taskId)
        {

            return Ok("{result: \"success\"}");
        }       
        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpPost]
        public IActionResult PostTask([Required] [FromBody] TaskCreationDto taskCreation)
        {
            var taskId = taskCreator.Create(taskCreation, out var errorString);
            if (errorString != null)
                return BadRequest(errorString);
            return Ok(new
            {
                taskId
            });
        }
        
        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpGet("task-id")]
        public IActionResult GetTaskByID([Required] [FromQuery(Name = "id")] Guid taskId)
        {
            return Ok(taskHandler.GetTask(taskId));
        }

        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpPost("delete")]
        public IActionResult PostTask([Required] [FromQuery(Name = "id")] Guid taskId)
        {
            taskHandler.DeleteTask(taskId);
            return Ok();
        }
        
        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpPost("{id}")]
        public IActionResult PostTaskTestsCode([Required] [FromRoute(Name = "id")] Guid taskId, [Required] [FromBody] TestsCodeDto testsCodeDto)
        {
            return taskHandler.TryChangeTaskTestsCode(taskId, testsCodeDto.TestsCode) ? Ok() : BadRequest();
        }

        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpGet($"{Solution}")]
        public IActionResult GetTaskSolutionInfo([Required] [FromQuery(Name = "id")] string taskSolutionId)
        {
            var taskSolutionInfo = taskHelper.GetTaskSolutionInfo(taskSolutionId, out var errorString);
            if (errorString != null)
                return BadRequest(errorString);
            if (taskSolutionInfo == null)
                return BadRequest("no task solution with such id or user solution refers to doesn't exist");
            return Ok(taskSolutionInfo);
        }
        
        [Authorize(Roles = "Interviewer,HrManager,Admin")]
        [HttpPut($"{Solution}/grade")]
        public IActionResult PutTaskSolutionGrade([Required] [FromQuery(Name = "id")] string taskSolutionId, [Required] [FromQuery(Name = "grade")] int grade)
        {
            if (!Enum.IsDefined(typeof(Grade), grade))
                return BadRequest($"{nameof(grade)} is invalid");
            
            if (!taskHelper.TryPutTaskSolutionGrade(taskSolutionId, (Grade)grade, out var errorString) || errorString != null)
                return BadRequest(errorString);
            return Ok();
        }
        
        [Authorize]
        [HttpPost($"{Solution}/reduce-attempt")]
        public IActionResult ReduceTaskSolutionAttempt([Required] [FromQuery(Name = "id")] string taskSolutionId)
        {
            if (!taskHelper.TryReduceTaskSolutionAttempt(taskSolutionId, out var errorString, out var runAttemptsLeft) || errorString != null)
                return BadRequest(errorString);
            return Ok(new
            {
                runAttemptsLeft
            });
        }
    }
}
