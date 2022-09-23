﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Organization
    {
        public Guid Id { get; set; }
        public String Name { get; set; }
        public List<Activity> Activities { get; set; }
    }
}
