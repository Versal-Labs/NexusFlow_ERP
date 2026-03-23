using MediatR;
using Microsoft.AspNetCore.Identity;
using NexusFlow.AppCore.DTOs.HR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Commands
{
    public class CreateEmployeeCommand : IRequest<Result<int>>
    {
        public EmployeeDto Employee { get; set; }
        public CreateEmployeeCommand(EmployeeDto employee) => Employee = employee;
    }

    public class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly INumberSequenceService _sequenceService;
        private readonly INotificationService _notificationService;

        public CreateEmployeeHandler(
            IErpDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            INumberSequenceService sequenceService,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _sequenceService = sequenceService;
            _notificationService = notificationService;
        }

        public async Task<Result<int>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Employee;

            // 1. Validation
            if (await _context.Employees.AnyAsync(e => e.Email == dto.Email, cancellationToken))
                return Result<int>.Failure("An employee with this email already exists.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. Map Entity
                var employee = new Employee
                {
                    EmployeeCode = await _sequenceService.GenerateNextNumberAsync("EMP", cancellationToken),
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    NIC = dto.NIC,
                    BasicSalary = dto.BasicSalary,
                    EPF_No = dto.EPF_No,
                    BankName = dto.BankName,
                    BankAccountNo = dto.BankAccountNo,
                    IsSalesRep = dto.IsSalesRep
                };

                // 3. ENTERPRISE AUTO-PROVISIONING
                if (employee.IsSalesRep)
                {
                    // Ensure the SalesRep role exists
                    if (!await _roleManager.RoleExistsAsync("SalesRep"))
                        await _roleManager.CreateAsync(new IdentityRole("SalesRep"));

                    // Generate a secure random password (e.g., Nx-8f2a1b!)
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

                    if (!identityResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Identity Provisioning Failed: {string.Join(", ", identityResult.Errors.Select(e => e.Description))}");
                    }

                    await _userManager.AddToRoleAsync(user, "SalesRep");

                    // Link the ERP Employee to the Identity User
                    employee.ApplicationUserId = user.Id;

                    //TO DO: Send email with credentials (can be done via background job or directly here)
                    // Trigger background email to the rep with their credentials
                    //await _notificationService.SendEmailAsync(
                    //    employee.Email,
                    //    "Nexus ERP - Your Sales Rep Account",
                    //    $"Hello {employee.FirstName},<br/>Your account is ready. <br/>Username: {employee.Email}<br/>Password: <b>{tempPassword}</b>"
                    //);
                }

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(employee.Id, $"Employee {employee.EmployeeCode} created successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Failed to create employee: {ex.Message}");
            }
        }
    }
}
