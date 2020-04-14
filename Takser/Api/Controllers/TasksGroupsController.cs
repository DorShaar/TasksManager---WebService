﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskData.Contracts;
using Tasker.App.Resources;
using Tasker.App.Services;
using Tasker.Domain.Communication;
using Tasker.Domain.Extensions;

namespace Takser.Api.Controllers
{
    [Route("api/[controller]")]
    public class TasksGroupsController : Controller
    {
        private readonly ITasksGroupService mTasksGroupService;
        private readonly IMapper mMapper;

        public TasksGroupsController(ITasksGroupService taskService, IMapper mapper)
        {
            mTasksGroupService = taskService;
            mMapper = mapper;
        }

        [HttpGet("[action]")]
        public async Task<IEnumerable<TasksGroupResource>> Groups()
        {
            IEnumerable<ITasksGroup> groups = await mTasksGroupService.ListAsync();
            return mMapper
                .Map<IEnumerable<ITasksGroup>, IEnumerable<TasksGroupResource>>(groups);
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] SaveTasksGroupResource resource)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrorMessages());

            ITasksGroup group = mMapper.Map<SaveTasksGroupResource, ITasksGroup>(resource);

            TasksGroupResponse result = await mTasksGroupService.SaveAsync(group);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            TasksGroupResource tasksGroupResource = mMapper.Map<ITasksGroup, TasksGroupResource>(result.TasksGroup);
            return Ok(tasksGroupResource);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAsync(string id, [FromBody] SaveTasksGroupResource resource)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrorMessages());

            ITasksGroup tasksGroup = mMapper.Map<SaveTasksGroupResource, ITasksGroup>(resource);
            TasksGroupResponse result = await mTasksGroupService.UpdateAsync(id, tasksGroup.Name);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            TasksGroupResource tasksGroupResource = mMapper.Map<ITasksGroup, TasksGroupResource>(result.TasksGroup);
            return Ok(tasksGroupResource);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveGroupAsync(string id)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState.GetErrorMessages());

            TasksGroupResponse result = await mTasksGroupService.RemoveAsync(id);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            TasksGroupResource tasksGroupResource = mMapper.Map<ITasksGroup, TasksGroupResource>(result.TasksGroup);
            return Ok(tasksGroupResource);
        }
    }
}