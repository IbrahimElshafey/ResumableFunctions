using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for sending commands or messages to external systems or services.
    /// </summary>
    public interface ICommandSender
    {
        Task<SendCommandResult> SendCommand(Command command, ServiceAddress send);
        Task<SendCommandResult<CommandResponse>> SendCommand<CommandResponse>(Command command, ServiceAddress send);
    }
}
