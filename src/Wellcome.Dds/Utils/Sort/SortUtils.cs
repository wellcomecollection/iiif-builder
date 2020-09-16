using System;
using System.Collections.Generic;

namespace Utils.Sort
{
    public static class SortUtils
    {        
        // This was the old utility method for arrays...
        public static void ShiftElement<T>(this T[] array, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= array.Length || newIndex < 0 || newIndex >= array.Length)
            {
                return;
            }
            if (oldIndex == newIndex)
            {
                return; // No-op
            }
            T tmp = array[oldIndex];
            if (newIndex < oldIndex)
            {
                // Need to move part of the array "up" to make room
                Array.Copy(array, newIndex, array, newIndex + 1, oldIndex - newIndex);
            }
            else
            {
                // Need to move part of the array "down" to fill the gap
                Array.Copy(array, oldIndex + 1, array, oldIndex, newIndex - oldIndex);
            }
            array[newIndex] = tmp;
        }
        
        
        // and this is it adapted for lists:
        public static void ShiftElement<T>(this List<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= list.Count || newIndex < 0 || newIndex >= list.Count)
            {
                return;
            }
            if (oldIndex == newIndex)
            {
                return; // No-op
            }
            T tmp = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, tmp);
        }
        
    }
}