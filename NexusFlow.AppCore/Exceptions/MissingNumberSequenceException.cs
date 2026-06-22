namespace NexusFlow.AppCore.Exceptions
{
    public sealed class MissingNumberSequenceException : InvalidOperationException
    {
        public MissingNumberSequenceException(string module)
            : base($"Required number sequence '{module}' is missing. Ask a system administrator to run the sequence health repair.")
        {
            Module = module;
        }

        public string Module { get; }
    }
}
