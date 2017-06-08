﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.MultiNodeTestRunner.Shared.Reporting
{
    public class TeamCityLoggerActor : ReceiveActor
    {
        public TeamCityLoggerActor()
        {
            ReceiveAny(o =>
            {
                Console.WriteLine(o.ToString() + Environment.NewLine);
            });
        }
    }
}
