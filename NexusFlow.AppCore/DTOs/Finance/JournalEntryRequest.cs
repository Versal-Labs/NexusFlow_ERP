using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Finance
{
    public class JournalEntryRequest
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public List<JournalLineRequest> Lines { get; set; } = new();
    }

    public class JournalLineRequest
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Note { get; set; }
    }
}
