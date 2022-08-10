using System.Threading.Tasks;

namespace Chord.Lib.Impl;

public class ZeroProtocolPayloadWorker : IChordPayloadWorker
{
    public async Task BackupData(IChordEndpoint successor)
        => await Task.Delay(100);

    public async Task PreloadData(IChordEndpoint successor)
        => await Task.Delay(100);
}
