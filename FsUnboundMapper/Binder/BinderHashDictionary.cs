using FsUnboundMapper.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FsUnboundMapper.Binder
{
    public class BinderHashDictionary : Dictionary<ulong, string>
    {
        /// <summary>
        /// The prime for computing 32-bit hashes.
        /// </summary>
        public const uint BINDER_PATH_PRIME_32 = 37u;

        /// <summary>
        /// The prime for computing 64-bit hashes.
        /// </summary>
        public const ulong BINDER_PATH_PRIME_64 = 133ul;

        /// <summary>
        /// Whether or not hashes are 64-bit.
        /// </summary>
        public bool Hashes64Bit { get; init; }

        /// <summary>
        /// Create a <see cref="BinderHashDictionary"/> with the given starting capacity and set whether or not hashes are 64-bit.
        /// </summary>
        public BinderHashDictionary(int capacity, bool bit64) : base(capacity)
        {
            Hashes64Bit = bit64;
        }

        /// <summary>
        /// Create a <see cref="BinderHashDictionary"/> and set whether or not hashes are 64-bit.
        /// </summary>
        public BinderHashDictionary(bool bit64) : this(0, bit64) { }

        /// <summary>
        /// Create a <see cref="BinderHashDictionary"/> and set whether or not hashes are 64-bit.
        /// </summary>
        public BinderHashDictionary() : this(0, false) { }

        /// <summary>
        /// Computes the hash of a file path string.
        /// </summary>
        /// <param name="value">The file path string to compute the hash of.</param>
        /// <returns>The hash of a file path string.</returns>
        public ulong ComputeHash(string value)
        {
            string hashable = value.Trim().Replace('\\', '/').ToLowerInvariant();
            if (!hashable.StartsWith('/'))
            {
                hashable = '/' + hashable;
            }

            return Hashes64Bit ? hashable.Aggregate(0ul, (i, c) => i * BINDER_PATH_PRIME_64 + c) : hashable.Aggregate(0u, (i, c) => i * BINDER_PATH_PRIME_32 + c);
        }

        /// <summary>
        /// Computes the hash of a file path string.
        /// </summary>
        /// <param name="value">The file path string to compute the hash of.</param>
        /// <param name="bit64">Whether or not hashes are 64-bit.</param>
        /// <returns>The hash of a file path string.</returns>
        public static ulong ComputeHash(string value, bool bit64)
        {
            string hashable = value.Trim().Replace('\\', '/').ToLowerInvariant();
            if (!hashable.StartsWith('/'))
            {
                hashable = '/' + hashable;
            }

            return bit64 ? hashable.Aggregate(0ul, (i, c) => i * BINDER_PATH_PRIME_64 + c) : hashable.Aggregate(0u, (i, c) => i * BINDER_PATH_PRIME_32 + c);
        }

        /// <summary>
        /// Checks whether or not two values' hashes are the same.
        /// </summary>
        /// <param name="valueA">The first value.</param>
        /// <param name="valueB">The second value.</param>
        /// <returns>Whether or not these two values' hashes collide.</returns>
        public bool Collides(string valueA, string valueB)
        {
            if (valueA.Equals(valueB))
            {
                return true;
            }

            return ComputeHash(valueA, Hashes64Bit) == ComputeHash(valueB, Hashes64Bit);
        }

        /// <summary>
        /// Checks whether or not two values' hashes are the same.
        /// </summary>
        /// <param name="valueA">The first value.</param>
        /// <param name="valueB">The second value.</param>
        /// <param name="bit64">Whether or not hashes are 64-bit.</param>
        /// <returns>Whether or not these two values' hashes collide.</returns>
        public static bool Collides(string valueA, string valueB, bool bit64)
        {
            if (valueA.Equals(valueB))
            {
                return true;
            }

            return ComputeHash(valueA, bit64) == ComputeHash(valueB, bit64);
        }

        #region Dictionary Methods

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="HashCollisionException">A hash collision occurred.</exception>
        /// <exception cref="DuplicateException">A given value already exists in the dictionary.</exception>
        public void Add(string value)
        {
            var hash = ComputeHash(value, Hashes64Bit);
            if (!TryAdd(hash, value))
            {
                var originalValue = this[hash];
                if (originalValue != value)
                {
                    throw new HashCollisionException($"A hash collision has been detected for two different values: Hash: {hash}; Values: {originalValue}; {value}");
                }

                throw new DuplicateException($"Value has already been added: Hash: {hash}; Value: {value}");
            }
        }

        /// <summary>
        /// Removes the specified value by searching the dictionary with it's computed hash.
        /// </summary>
        /// <param name="value">The value to remove.</param>
        /// <returns>Returns <see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.  This method returns <see langword="false" /> if <paramref name="value" /> is not found in the <see cref="BinderHashDictionary" />.</returns>
        public bool Remove(string value)
        {
            return Remove(ComputeHash(value, Hashes64Bit));
        }

        /// <summary>
        /// Attempts to add the specified value to the <see cref="BinderHashDictionary"/>.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns>Whether or not adding the value was successful.</returns>
        public bool TryAdd(string value)
        {
            return TryAdd(ComputeHash(value, Hashes64Bit), value);
        }

        /// <summary>
        /// Attempts to add the specified value to the <see cref="BinderHashDictionary"/>.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <param name="hash">The hash generated from the attempt.</param>
        /// <returns>Whether or not adding the value was successful.</returns>
        public bool TryAdd(string value, out ulong hash)
        {
            hash = ComputeHash(value, Hashes64Bit);
            return TryAdd(hash, value);
        }

        /// <summary>
        /// Add a range of values to the <see cref="BinderHashDictionary"/>.
        /// </summary>
        /// <param name="values">The values to add.</param>
        public void AddRange(string[] values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        /// <summary>
        /// Add a range of values to the <see cref="BinderHashDictionary"/>.
        /// </summary>
        /// <param name="values">The values to add.</param>
        public void AddRange(IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        /// <summary>
        /// Add a range of values to the <see cref="BinderHashDictionary"/>.
        /// </summary>
        /// <param name="values">The values to add.</param>
        public void AddRange(ReadOnlySpan<string> values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        /// <summary>
        /// Determines whether or not the <see cref="BinderHashDictionary"/> contains the specified value.
        /// </summary>
        /// <param name="value">The value to check for.</param>
        /// <returns>Returns <see langword="true" /> if the <see cref="BinderHashDictionary"/> contains an element with the specified value; otherwise, <see langword="false" />.</returns>
        public bool ContainsHashableValue(string value)
            => ContainsKey(ComputeHash(value, Hashes64Bit));

        #endregion
    }
}
