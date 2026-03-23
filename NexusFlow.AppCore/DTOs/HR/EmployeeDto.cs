using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.HR
{
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }
        public string EPF_No { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNo { get; set; } = string.Empty;

        public bool IsSalesRep { get; set; }
    }
}
