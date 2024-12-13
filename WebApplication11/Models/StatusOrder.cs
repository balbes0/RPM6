﻿using System;
using System.Collections.Generic;

namespace WebApplication11.Models;

public partial class StatusOrder
{
    public int IdStatus { get; set; }

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
