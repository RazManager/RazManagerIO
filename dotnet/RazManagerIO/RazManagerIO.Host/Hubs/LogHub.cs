using Microsoft.AspNetCore.SignalR;
using RazManagerIO.Host.Services.MemoryLogger;
using System.Threading.Tasks;


namespace RazManagerIO.Host.Hubs
{
    public interface ILogHub
    {
        Task ChangedState(MemoryLoggerData dto);
    }


    public class LogHub : Hub<ILogHub>
    {
    }
}
