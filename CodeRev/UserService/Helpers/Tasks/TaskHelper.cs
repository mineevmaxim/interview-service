﻿using System;
using System.Collections.Generic;
using System.Linq;
using UserService.DAL.Entities;
using UserService.DAL.Models.Enums;
using UserService.DAL.Models.Interfaces;
using UserService.Helpers.Auth;
using UserService.Models.Contest;
using UserService.Models.Review;
using Task = UserService.DAL.Entities.Task;

namespace UserService.Helpers.Tasks
{
    public interface ITaskHelper
    {
        TaskSolutionInfo GetTaskSolutionInfo(string taskSolutionId, out string errorString);
        TaskSolutionInfo GetTaskSolutionInfo(Guid taskSolutionId);
        List<TaskSolution> GetTaskSolutions(Guid interviewSolutionId);
        List<TaskSolutionInfoContest> GetTaskSolutionInfosForContest(string interviewSolutionId, out string errorString);
        TaskSolution GetTaskSolution(string taskSolutionId, out string errorString);
        bool TryPutTaskSolutionGrade(string taskSolutionId, Grade grade, out string errorString);
        bool EndTaskSolution(string taskSolutionId, out string errorString);
        List<Task> GetAllTasks();
        bool TryReduceTaskSolutionAttempt(string taskSolutionId, out string errorString, out int runAttemptsLeft);
    }

    public class TaskHelper : ITaskHelper
    {
        private readonly IDbRepository dbRepository;
        private readonly ITaskHandler taskHandler;
        private readonly IUserHelper userHelper;

        public TaskHelper(IDbRepository dbRepository, IUserHelper userHelper, ITaskHandler taskHandler)
        {
            this.dbRepository = dbRepository;
            this.userHelper = userHelper;
            this.taskHandler = taskHandler;
        }

        public TaskSolutionInfo GetTaskSolutionInfo(string taskSolutionId, out string errorString)
        {
            (var taskSolutionGuid, errorString) = GuidParser.TryParse(taskSolutionId, nameof(taskSolutionId));
            return errorString == null ? GetTaskSolutionInfo(taskSolutionGuid) : null;
        }

        public TaskSolutionInfo GetTaskSolutionInfo(Guid taskSolutionId)
        {
            var taskSolution = taskHandler.GetTaskSolution(taskSolutionId);
            if (taskSolution == null)
                return null;
            
            var userFullName = userHelper.GetFullNameByInterviewSolutionId(taskSolution.InterviewSolutionId);
            if (userFullName == null)
                return null;
            
            return new TaskSolutionInfo
            {
                TaskSolutionId = taskSolution.Id,
                TaskId = taskSolution.TaskId,
                TaskOrder = 0,
                InterviewSolutionId = taskSolution.InterviewSolutionId,
                FullName = userFullName,
                Grade = taskSolution.Grade,
                IsDone = taskSolution.IsDone,
                RunAttemptsLeft = taskSolution.RunAttemptsLeft,
                TaskName = taskHandler.GetTask(taskSolution.TaskId).Name,
                ProgrammingLanguage = taskHandler.GetTask(taskSolution.TaskId).ProgrammingLanguage,
            };
        }

        public List<TaskSolution> GetTaskSolutions(Guid interviewSolutionId)
            => dbRepository.Get<TaskSolution>(t => t.InterviewSolutionId == interviewSolutionId).ToList();

        public List<TaskSolutionInfoContest> GetTaskSolutionInfosForContest(string interviewSolutionId, out string errorString)
        {
            (var interviewSolutionGuid, errorString) = GuidParser.TryParse(interviewSolutionId, nameof(interviewSolutionId));
            if (errorString != null)
                return null;

            var letterOrder = 1;
            var taskInfos = GetTaskSolutions(interviewSolutionGuid)
                .Join(dbRepository.Get<Task>(),
                    tSln => tSln.TaskId,
                    t => t.Id,
                    (tSln, t) => new TaskSolutionInfoContest
                    {
                        Id = tSln.Id,
                        TaskId = tSln.TaskId,
                        TaskText = t.TaskText,
                        TaskName = t.Name,
                        StartCode = t.StartCode,
                        IsDone = tSln.IsDone,
                        RunAttemptsLeft = tSln.RunAttemptsLeft,
                        ProgrammingLanguage = t.ProgrammingLanguage,
                    })
                .OrderBy(t => t.TaskId)
                .Select(t =>
                {
                    t.TaskOrder = letterOrder++;
                    return t;
                })
                .ToList();
            
            return taskInfos;
        }

        public TaskSolution GetTaskSolution(string taskSolutionId, out string errorString)
        {
            (var taskSolutionGuid, errorString) = GuidParser.TryParse(taskSolutionId, nameof(taskSolutionId));
            return errorString == null ? taskHandler.GetTaskSolution(taskSolutionGuid) : null;
        }

        public bool TryPutTaskSolutionGrade(string taskSolutionId, Grade grade, out string errorString)
        {
            (var taskSolutionGuid, errorString) = GuidParser.TryParse(taskSolutionId, nameof(taskSolutionId));
            if (errorString != null)
                return false;
            var taskSolution = taskHandler.GetTaskSolution(taskSolutionGuid);
            if (taskSolution == null)
            {
                errorString = $"no {nameof(taskSolution)} with such id";
                return false;
            }
            if (taskSolution.IsDone == false)
            {
                errorString = $"{nameof(taskSolution)} wasn't done yet";
                return false;
            }
            
            taskSolution.Grade = grade;
            dbRepository.SaveChangesAsync().Wait();
            return true;
        }

        public bool EndTaskSolution(string taskSolutionId, out string errorString)
        {
            var taskSolution = GetTaskSolution(taskSolutionId, out errorString);
            if (taskSolution == null)
            {
                errorString = $"no {nameof(taskSolution)} with such id";
                return false;
            }

            if (taskSolution.IsDone)
            {
                errorString = $"{nameof(taskSolution)} is already done";
                return false;
            }
            var interviewSolution = dbRepository //чтобы не было циклической зависимости при создании, приходится вручную доставать (мб через Lazy пофиксить)
                .Get<InterviewSolution>(i => i.Id == taskSolution.InterviewSolutionId)
                .FirstOrDefault();
            if (interviewSolution == null)
            {
                errorString = $"no {nameof(interviewSolution)} with such id";
                return false;
            }

            if (DateTimeOffset.Now.ToUnixTimeMilliseconds() > interviewSolution.EndTimeMs)
            {
                errorString = $"{nameof(interviewSolution)} is already end (end time is less than now time) or wasn't started";
                return false;
            }

            taskSolution.IsDone = true;
            dbRepository.SaveChangesAsync().Wait();
            return true;
        }

        public List<Task> GetAllTasks()
            => dbRepository.Get<Task>(i => i.IsDeleted == false).ToList();

        public bool TryReduceTaskSolutionAttempt(string taskSolutionId, out string errorString, out int runAttemptsLeft)
        {
            runAttemptsLeft = 0;
            (var taskSolutionGuid, errorString) = GuidParser.TryParse(taskSolutionId, nameof(taskSolutionId));
            if (errorString != null)
                return false;
            var taskSolution = taskHandler.GetTaskSolution(taskSolutionGuid);
            if (taskSolution == null)
            {
                errorString = $"no {nameof(taskSolution)} with such id";
                return false;
            }

            if (taskSolution.RunAttemptsLeft == 0)
                return true;

            taskSolution.RunAttemptsLeft -= 1;
            dbRepository.SaveChangesAsync().Wait();
            runAttemptsLeft = taskSolution.RunAttemptsLeft;
            return true;
        }
    }
}