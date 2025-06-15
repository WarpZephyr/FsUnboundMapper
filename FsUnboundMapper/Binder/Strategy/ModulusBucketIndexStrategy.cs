namespace FsUnboundMapper.Binder.Strategy
{

    public class ModulusBucketIndexStrategy(int bucketCount) : IBucketIndexStrategy
    {
        /// <summary>
        /// The total number of buckets.
        /// </summary>
        public int BucketCount { get; set; } = bucketCount;

        public int ComputeBucketIndex(ulong hash)
        {
            return (int)(hash % (ulong)BucketCount);
        }
    }
}
