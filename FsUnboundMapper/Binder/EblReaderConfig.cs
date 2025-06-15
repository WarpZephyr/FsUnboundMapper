using FsUnboundMapper.Binder.Strategy;

namespace FsUnboundMapper.Binder
{
    public class EblReaderConfig
    {
        public required BinderHashDictionary NameDictionary { get; init; }
        public required IBucketIndexStrategy IndexingStrategy { get; init; }
        public bool LeaveDataOpen { get; init; }
    }
}
