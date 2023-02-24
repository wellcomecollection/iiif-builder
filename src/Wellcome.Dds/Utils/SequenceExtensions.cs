using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Utils
{
    public static class SequenceExtensions
    {
        /// <summary>
        /// F# has DistinctBy but C# doesn't. So here it is.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Does the sequence contain anything (allows null sequence)?
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? enumerable) 
            => enumerable == null || !enumerable.Any();

        /// <summary>
        /// Does the sequence contain anything (allows null sequence)?
        /// </summary>
        public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IList<T>? enumerable) 
            => enumerable == null || enumerable.Count == 0;

        /// <summary>
        /// Does the sequence contain anything (allows null sequence)?
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static bool HasItems<T>([NotNullWhen(true)] this IEnumerable<T>? enumerable) => !enumerable.IsNullOrEmpty();

        /// <summary>
        /// Does the sequence contain anything (allows null sequence)?
        /// </summary>
        public static bool HasItems<T>([NotNullWhen(true)] this IList<T>? enumerable) => !enumerable.IsNullOrEmpty();


        public static IEnumerable<T> AnyItems<T>(this IEnumerable<T>? items)
        {
            if (items == null)
            {
                return new List<T>(0);
            }

            return items;
        }
        /// <summary>
        /// Generate collection of IEnumerable of specified size.
        /// </summary>
        /// <remarks>From morelinq. Consider importing whole library</remarks>
        public static IEnumerable<IEnumerable<T>> Batch<T>(
            this IEnumerable<T> source, int size)
        {
            T[]? bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                bucket ??= new T[size];
                bucket[count++] = item;

                if (count != size)
                    continue;

                yield return bucket.Select(x => x);

                bucket = null;
                count = 0;
            }

            // Return the last bucket with all remaining elements
            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }
    }
}