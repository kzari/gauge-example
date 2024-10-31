using System;
using System.Collections.Generic;
using System.Linq;

namespace AgileContent.Itaas.E2E.Models;

public class Notification
{

    public string Sender { get; set; }
    public string Status { get; set; }
    public string Message => Messages != null && Messages.Count > 0
        ? $"[{string.Join(',', Messages.Select(x => x))}]" :
        "";
    public List<string> Messages { get; set; }
    public DateTime Date { get; set; }

    public string DatabaseName { get; set; }

    public override string ToString()
    {
        return $"[{Sender}] => {Status}: {Message}";
    }
}