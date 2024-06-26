﻿using System;

namespace Utils.Guard
{
    public static class GuardX
    {
        /// <summary>
        /// Throw <see cref="ArgumentNullException"/> if provided string is null or empty.
        /// </summary>
        /// <param name="val">Value to check.</param>
        /// <param name="argName">Argument name for exception.</param>
        /// <returns>Provided value if not null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if val is null or empty.</exception>
        public static string ThrowIfNullOrEmpty(this string val, string argName = "argument")
        {
            if (string.IsNullOrEmpty(val))
            {
                throw new ArgumentNullException(argName);
            }

            return val;
        }
        
        /// <summary>
        /// Validates argument is not null.
        /// </summary>
        /// <param name="argument">Argument to check.</param>
        /// <param name="argName">Name of argument.</param>
        /// <typeparam name="T">Type of argument to check.</typeparam>
        /// <returns>Passed argument, if not null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if provided argument is null.</exception>
        public static T ThrowIfNull<T>(this T argument, string argName)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(argName);
            }

            return argument;
        }
    }
}