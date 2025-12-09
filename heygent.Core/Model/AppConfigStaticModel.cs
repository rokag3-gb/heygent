using heygent.Core.Model;
using heygent.Core.Helper;
using heygent.Core.Dto;

namespace heygent.Core;

public static class Conf
{
    public static AppConfig Current { get; set; } = new AppConfig();

    public static NetSnapshot CurrentNetInfo { get; set; } = new(); // new NetInfo().SnapshotAsync().GetAwaiter().GetResult();
}