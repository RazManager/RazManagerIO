using Microsoft.Extensions.Logging;


namespace RazManagerIO.Host.Services.MemoryLogger
{
    public class MemoryLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new MemoryLogger(categoryName);
        }


        public void Dispose()
        {
        }
    }
}
