using MediatR;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.AppCore.Features.Installation
{
    public sealed record ValidateInstallationDatabaseCommand(DatabaseConnectionRequest Database)
        : IRequest<DatabaseValidationResult>;

    public sealed class ValidateInstallationDatabaseHandler
        : IRequestHandler<ValidateInstallationDatabaseCommand, DatabaseValidationResult>
    {
        private readonly IInstallationDatabaseProvisioner _provisioner;

        public ValidateInstallationDatabaseHandler(IInstallationDatabaseProvisioner provisioner)
        {
            _provisioner = provisioner;
        }

        public Task<DatabaseValidationResult> Handle(
            ValidateInstallationDatabaseCommand request,
            CancellationToken cancellationToken) =>
            _provisioner.ValidateAsync(request.Database, cancellationToken);
    }

    public sealed record RunInstallationCommand(InstallationRequest Installation) : IRequest<InstallationResult>;

    public sealed class RunInstallationHandler : IRequestHandler<RunInstallationCommand, InstallationResult>
    {
        private readonly IInstallationOrchestrator _orchestrator;

        public RunInstallationHandler(IInstallationOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task<InstallationResult> Handle(RunInstallationCommand request, CancellationToken cancellationToken) =>
            _orchestrator.InstallAsync(request.Installation, cancellationToken);
    }

    public sealed record RunInstallationUpgradeCommand : IRequest<InstallationResult>;

    public sealed class RunInstallationUpgradeHandler : IRequestHandler<RunInstallationUpgradeCommand, InstallationResult>
    {
        private readonly IInstallationOrchestrator _orchestrator;

        public RunInstallationUpgradeHandler(IInstallationOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task<InstallationResult> Handle(RunInstallationUpgradeCommand request, CancellationToken cancellationToken) =>
            _orchestrator.UpgradeAsync(cancellationToken);
    }

    public sealed record GetInstallationReadinessQuery : IRequest<ReadinessReport>;

    public sealed class GetInstallationReadinessHandler : IRequestHandler<GetInstallationReadinessQuery, ReadinessReport>
    {
        private readonly IInstallationReadinessChecker _checker;

        public GetInstallationReadinessHandler(IInstallationReadinessChecker checker)
        {
            _checker = checker;
        }

        public Task<ReadinessReport> Handle(GetInstallationReadinessQuery request, CancellationToken cancellationToken) =>
            _checker.CheckAsync(cancellationToken);
    }
}
