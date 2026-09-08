using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace Lesson
{
    public class UptimeHub : Hub
    {
        public void Hello()
        {
            Clients.All.hello();
        }
    }
}