﻿using System;
using System.Collections.Generic;
using System.Linq;
using UserService.DAL.Entities;
using UserService.DAL.Models.Interfaces;
using UserService.Helpers.Auth;
using UserService.Helpers.Interviews;
using UserService.Models.SyncInterviews;

namespace UserService.Helpers
{
    public interface IMeetsHelper
    {
        public IEnumerable<MeetInfoDto> GetMeets(string requestingUserId);
        public IEnumerable<MeetInfoDto> GetUserMeets(string requestingUserId);
    }
    
    public class MeetsHelper : IMeetsHelper
    {
        private readonly IDbRepository dbRepository;
        private readonly IUserHelper userHelper;
        private readonly IInterviewHelper interviewHelper;
        private readonly IInterviewLanguageHandler interviewLanguageHandler;

        public MeetsHelper(IDbRepository dbRepository, IUserHelper userHelper, IInterviewHelper interviewHelper, IInterviewLanguageHandler interviewLanguageHandler)
        {
            this.dbRepository = dbRepository;
            this.userHelper = userHelper;
            this.interviewHelper = interviewHelper;
            this.interviewLanguageHandler = interviewLanguageHandler;
        }

        
        public IEnumerable<MeetInfoDto> GetUserMeets(string requestingUserId)
        {
            var requestingUser = userHelper.Get(requestingUserId, out _);
            if (requestingUser == null)
                return Enumerable.Empty<MeetInfoDto>();
            var time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            
            var tasksPerInterviews = dbRepository.Get<InterviewTask>()
                .ToList()
                .GroupBy(interviewTask => interviewTask.InterviewId)
                .Select(group => (group.Key, group.Count()));
            var meets = dbRepository.Get<InterviewSolution>()
                .Where(interviewSolution => !interviewSolution.IsSubmittedByCandidate && interviewSolution.IsSynchronous && time < interviewSolution.EndTimeMs )
                .ToList()
                .Join(dbRepository.Get<Interview>()
                    .ToList(),
                interviewSolution => interviewSolution.InterviewId,
                interview => interview.Id,
                (interviewSolution, interview) => new MeetInfoDto
                {
                    UserId = interviewSolution.UserId,
                    InterviewSolutionId = interviewSolution.Id,
                    InterviewId = interview.Id,
                    Vacancy = interview.Vacancy,
                    ProgrammingLanguages = interviewLanguageHandler.GetInterviewLanguages(interview.Id),
                    IsOwnerMeet = interviewSolution.UserId.Equals(requestingUser.Id),
                });
            
            meets = meets.Where(x => x.UserId == requestingUser.Id).Join(
                dbRepository.Get<User>()
                    .ToList(), 
                meet => meet.UserId, 
                user => user.Id,
                (meet, user) =>
                {
                    userHelper.GetFirstNameAndSurname(user, out var firstName, out var surname);
                    meet.FirstName = firstName;
                    meet.Surname = surname;
                    return meet;
                });

            meets = meets.Join(
                tasksPerInterviews,
                meet => meet.InterviewId,
                tasksPerInterview => tasksPerInterview.Key,
                (meet, tasksPerInterview) =>
                {
                    meet.TasksCount = tasksPerInterview.Item2;
                    return meet;
                });
            
            return meets;
        }
        
        public IEnumerable<MeetInfoDto> GetMeets(string requestingUserId)
        {
            var requestingUser = userHelper.Get(requestingUserId, out _);
            if (requestingUser == null)
                return Enumerable.Empty<MeetInfoDto>();
            var time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            
            var tasksPerInterviews = dbRepository.Get<InterviewTask>()
                .ToList()
                .GroupBy(interviewTask => interviewTask.InterviewId)
                .Select(group => (group.Key, group.Count()));
            var meets = dbRepository.Get<InterviewSolution>()
                .Where(interviewSolution => !interviewSolution.IsSubmittedByCandidate && interviewSolution.IsSynchronous && time < interviewSolution.EndTimeMs )
                .ToList()
                .Join(dbRepository.Get<Interview>()
                    .ToList(),
                interviewSolution => interviewSolution.InterviewId,
                interview => interview.Id,
                (interviewSolution, interview) => new MeetInfoDto
                {
                    UserId = interviewSolution.UserId,
                    InterviewSolutionId = interviewSolution.Id,
                    InterviewId = interview.Id,
                    Vacancy = interview.Vacancy,
                    ProgrammingLanguages = interviewLanguageHandler.GetInterviewLanguages(interview.Id),
                    IsOwnerMeet = interviewSolution.InvitedBy.Equals(requestingUser.Id),
                });
            
            meets = meets.Join(
                dbRepository.Get<User>()
                    .ToList(), 
                meet => meet.UserId, 
                user => user.Id,
                (meet, user) =>
                {
                    userHelper.GetFirstNameAndSurname(user, out var firstName, out var surname);
                    meet.FirstName = firstName;
                    meet.Surname = surname;
                    return meet;
                });

            meets = meets.Join(
                tasksPerInterviews,
                meet => meet.InterviewId,
                tasksPerInterview => tasksPerInterview.Key,
                (meet, tasksPerInterview) =>
                {
                    meet.TasksCount = tasksPerInterview.Item2;
                    return meet;
                });
            
            return meets;
        }
    }
}