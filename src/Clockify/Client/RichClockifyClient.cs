﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Clockify.Models;
using Clockify.Net;
using Clockify.Net.Models.Clients;
using Clockify.Net.Models.Projects;
using Clockify.Net.Models.Reports;
using Clockify.Net.Models.Tags;
using Clockify.Net.Models.Tasks;
using Clockify.Net.Models.TimeEntries;
using Clockify.Net.Models.Users;
using Clockify.Net.Models.Workspaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace Bot.Clockify.Client
{
    public class RichClockifyClient : IClockifyClient
    {
        private readonly ClockifyClient _clockifyClient;
        private readonly IRestClient _restClient;

        public RichClockifyClient(string apiKey)
        {
            _clockifyClient = new ClockifyClient(apiKey);
            
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Converters = (IList<JsonConverter>) new List<JsonConverter>()
                {
                    (JsonConverter) new StringEnumConverter(),
                    (JsonConverter) new IsoDateTimeConverter()
                    {
                        DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"
                    }
                },
                ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver()
            };
            
            
            _restClient = (IRestClient) new RestClient("https://reports.api.clockify.me/v1");
            _restClient.UseNewtonsoftJson(settings);
        }

        public Task<IRestResponse> DeleteTimeEntryAsync(string workspaceId, string timeEntryId)
        {
            return _clockifyClient.DeleteTimeEntryAsync(workspaceId, timeEntryId);
        }

        public Task<IRestResponse<List<ProjectDtoImpl>>> FindAllProjectsOnWorkspaceByClientsAsync(
            string workspaceId, IEnumerable<string> clients)
        {
            return _clockifyClient.FindAllProjectsOnWorkspaceAsync(workspaceId, clients: clients.ToArray());
        }

        public Task<IRestResponse<List<HydratedTimeEntryDtoImpl>>> FindAllHydratedTimeEntriesForUserAsync(
            string workspaceId, string userId, string? description = null,
            DateTimeOffset? start = null, DateTimeOffset? end = null, string? project = null, string? task = null,
            bool? projectRequired = null, bool? taskRequired = null, bool? considerDurationFormat = null,
            bool? inProgress = null, int page = 1, int pageSize = 50)
        {
            return _clockifyClient.FindAllHydratedTimeEntriesForUserAsync(workspaceId, userId, description, start, end,
                project, task, projectRequired, taskRequired, considerDurationFormat, inProgress, page, pageSize);
        }


        public async Task<IRestResponse<WorkspaceReportDo>> GetSummaryReportForWorkspace(DateTimeOffset start,
            DateTimeOffset end, string workspaceId, string clockifyApiKey)
        {

            var summaryReportRequest = new SummaryReportRequest
            {
                DateRangeStart = start,
                DateRangeEnd = end,
                SummaryFilter = new SummaryFilterDto
                {
                    Groups = new List<GroupType> { GroupType.PROJECT }
                }
            };

            var request = new RestRequest("workspaces/" + workspaceId + "/reports/summary", Method.POST);
            request.AddJsonBody((object) summaryReportRequest);
            
            _restClient.AddDefaultHeader("X-Api-Key", clockifyApiKey);

            var res =  await _restClient.ExecutePostAsync<WorkspaceReportDo>((IRestRequest) request);
            return res; 
        }


        public Task<IRestResponse<List<TaskDto>>> FindAllTasksAsync(string workspaceId, string projectId,
            bool? isActive = null, string? name = null, int page = 1,
            int pageSize = 50)
        {
            return _clockifyClient.FindAllTasksAsync(workspaceId, projectId, isActive, name, page, pageSize);
        }

        public Task<IRestResponse<TaskDto>> CreateTaskAsync(string workspaceId, string projectId,
            TaskRequest taskRequest)
        {
            return _clockifyClient.CreateTaskAsync(workspaceId, projectId, taskRequest);
        }

        public Task<IRestResponse<CurrentUserDto>> GetCurrentUserAsync()
        {
            return _clockifyClient.GetCurrentUserAsync();
        }

        public Task<IRestResponse<List<WorkspaceDto>>> GetWorkspacesAsync()
        {
            return _clockifyClient.GetWorkspacesAsync();
        }

        public Task<IRestResponse<List<ClientDto>>> FindAllClientsOnWorkspaceAsync(string workspaceId)
        {
            return _clockifyClient.FindAllClientsOnWorkspaceAsync(workspaceId);
        }

        public Task<IRestResponse<List<ProjectDtoImpl>>> FindAllProjectsOnWorkspaceAsync(
            string workspaceId,
            int page = 1,
            int pageSize = 50)
        {
            return _clockifyClient.FindAllProjectsOnWorkspaceAsync(workspaceId, page: page, pageSize: pageSize);
        }

        public Task<IRestResponse<List<TagDto>>> FindAllTagsOnWorkspaceAsync(string workspaceId)
        {
            return _clockifyClient.FindAllTagsOnWorkspaceAsync(workspaceId);
        }

        public Task<IRestResponse<TimeEntryDtoImpl>> CreateTimeEntryAsync(string workspaceId,
            TimeEntryRequest timeEntryRequest)
        {
            return _clockifyClient.CreateTimeEntryAsync(workspaceId, timeEntryRequest);
        }
        
    }
}