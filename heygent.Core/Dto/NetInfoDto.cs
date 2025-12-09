using System.Net;

namespace heygent.Core.Dto;

public readonly record struct NetSnapshot(
    string HostName,
    IPAddress? PrivateIPv4,
    IPAddress? PrivateIPv6,
    IPAddress? PublicIPv4
);