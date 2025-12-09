using heygent.Core.Dto;

namespace heygent.Core.Ipc;

public interface INamedPipeServer
{
    void Start();
    void Stop();
}

public interface INamedPipeClient
{
    Task<PongResponse?> SendPingAsync(PingRequest req);
}