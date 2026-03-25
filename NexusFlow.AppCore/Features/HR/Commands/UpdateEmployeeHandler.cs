using MediatR;
using Microsoft.AspNetCore.Identity;
using NexusFlow.AppCore.DTOs.HR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Commands
{
    public class UpdateEmployeeCommand : IRequest<Result<int>>
    {
        public EmployeeDto Employee { get; set; }
        public UpdateEmployeeCommand(EmployeeDto employee) => Employee = employee;
    }

    public class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly INotificationService _notificationService;

        public UpdateEmployeeHandler(
            IErpDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _notificationService = notificationService;
        }

        public async Task<Result<int>> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Employee;
            var employee = await _context.Employees.FindAsync(new object[] { dto.Id }, cancellationToken);

            if (employee == null) return Result<int>.Failure("Employee not found.");

            // Ensure email uniqueness if they changed it
            if (employee.Email != dto.Email && await _context.Employees.AnyAsync(e => e.Email == dto.Email, cancellationToken))
            {
                return Result<int>.Failure("Email already in use by another employee.");
            }

            // Did they just turn ON the Sales Rep flag for an existing employee?
            bool newlyAssignedSalesRep = !employee.IsSalesRep && dto.IsSalesRep;

            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Email = dto.Email;
            employee.Phone = dto.Phone;
            employee.NIC = dto.NIC;
            employee.BasicSalary = dto.BasicSalary;
            employee.EPF_No = dto.EPF_No;
            employee.BankName = dto.BankName;
            employee.BankAccountNo = dto.BankAccountNo;
            employee.IsSalesRep = dto.IsSalesRep;

            // ARCHITECTURAL GUARD: Late Provisioning
            if (newlyAssignedSalesRep && string.IsNullOrEmpty(employee.ApplicationUserId))
            {
                if (!await _roleManager.RoleExistsAsync("SalesRep"))
                    await _roleManager.CreateAsync(new IdentityRole("SalesRep"));

                string tempPassword = $"Nx-{Guid.NewGuid().ToString().Substring(0, 6)}!";
                var user = new ApplicationUser
                {
                    UserName = employee.Email,
                    Email = employee.Email,
                    FullName = $"{employee.FirstName} {employee.LastName}",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var identityResult = await _userManager.CreateAsync(user, tempPassword);
                if (!identityResult.Succeeded) throw new Exception("Identity Provisioning Failed.");

                await _userManager.AddToRoleAsync(user, "SalesRep");
                employee.ApplicationUserId = user.Id;

                //await _notificationService.SendEmailAsync(
                //    employee.Email, "Nexus ERP - Sales Rep Access",
                //    $"Hello {employee.FirstName},<br/>Your account is ready. <br/>Username: {employee.Email}<br/>Password: <b>{tempPassword}</b>"
                //);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(employee.Id, "Employee updated successfully.");
        }
    }
}
