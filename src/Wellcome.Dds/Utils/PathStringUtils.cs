using System;

namespace Utils
{
    public static class PathStringUtils
    {
        /// <summary>
        /// If we know the path of an item we also know what its simpleName must be:
        /// 
        /// /one/two/three => three
        /// /one/two/three/ => three
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string? GetSimpleNameFromPath(string path)
        {
            if (path.EndsWith("/"))
            {
                path = path[..^1];
            }
            int spos = path.LastIndexOf("/", StringComparison.Ordinal) + 1;
            var simpleName = path.Substring(spos);
            return simpleName;
        }
    }
}
