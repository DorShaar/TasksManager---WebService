﻿using TaskManagerWebService.Domain.Models;

namespace TaskManagerWebService.Resources
{
    public class WorkTaskResource
    {
        public string TaskId { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
    }
}