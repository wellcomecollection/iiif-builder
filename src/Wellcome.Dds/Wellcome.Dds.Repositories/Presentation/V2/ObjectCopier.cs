using System;
using DeepCopy;

namespace Wellcome.Dds.Repositories.Presentation.V2
{
    /// <summary>
    /// Helper methods for creating copies of objects.
    /// </summary>
    public static class ObjectCopier
    {
        /// <summary>
        /// Create a deep copy of provided object.
        /// </summary>
        /// <param name="source">Object to be copied.</param>
        /// <param name="postCopyModifier">Optional function to call, takes newly created object as argument.</param>
        /// <typeparam name="T">Type of object to be copied</typeparam>
        /// <returns>Newly created copy of object.</returns>
        public static T? DeepCopy<T>(T source, Action<T>? postCopyModifier = null)
        {
            var copy = DeepCopier.Copy(source);
            postCopyModifier?.Invoke(copy);
            return copy;
        }
    }
}