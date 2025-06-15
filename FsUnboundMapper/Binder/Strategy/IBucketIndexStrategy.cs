namespace FsUnboundMapper.Binder.Strategy
{
    /// <summary>
    /// An implementation for strategies that get a bucket index. 
    /// </summary>
    public interface IBucketIndexStrategy
    {
        /// <summary>
        /// Gets a bucket index according to the implemented strategy.
        /// </summary>
        /// <param name="hash">The hash to get the bucket index of.</param>
        /// <returns>A bucket index.</returns>
        public int ComputeBucketIndex(ulong hash);
    }
}
