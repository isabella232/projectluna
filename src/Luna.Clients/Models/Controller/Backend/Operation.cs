﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Luna.Clients.Models.Controller.Backend
{
    public class Operation
    {
        public string operationType { get; set; }
        public string modelId { get; set; }
        public string status { get; set; }
        public string startTimeUtc { get; set; }
        public string completeTimeUtc { get; set; }
        public string description { get; set; }
        public object error { get; set; }
    }
}
