using System.Collections.Generic;
using FluentAssertions;
using Utils.Sort;
using Xunit;

namespace Utils.Tests
{
    public class SortTests
    {
        [Fact]
        public void ArrayShiftElementTests()
        {
            string[] array = new[] {"a", "b", "c", "d"};
            array.ShiftElement(1, 3);
            AssertOrder(array, "a", "c", "d", "b");

            array = new[] { "a", "b", "c", "d" };
            array.ShiftElement(3, 3);
            AssertOrder(array, "a", "b", "c", "d");

            array = new[] { "a", "b", "c", "d" };
            array.ShiftElement(0, 1);
            AssertOrder(array, "b", "a", "c", "d");

            array = new[] { "a", "b", "c", "d" };
            array.ShiftElement(0, 3);
            AssertOrder(array, "b", "c", "d", "a");
        }
        
        [Fact]
        public void ListShiftElementTests()
        {
            List<string> list;
            list = new List<string>{"a", "b", "c", "d"};
            list.ShiftElement(1, 3);
            AssertOrder(list.ToArray(), "a", "c", "d", "b");

            list = new List<string>{ "a", "b", "c", "d" };
            list.ShiftElement(3, 3);
            AssertOrder(list.ToArray(), "a", "b", "c", "d");

            list = new List<string>{ "a", "b", "c", "d" };
            list.ShiftElement(0, 1);
            AssertOrder(list.ToArray(), "b", "a", "c", "d");

            list = new List<string>{ "a", "b", "c", "d" };
            list.ShiftElement(0, 3);
            AssertOrder(list.ToArray(), "b", "c", "d", "a");
        }
        
        private void AssertOrder(string[] array, params string[] items)
        {
            array.Length.Should().Be(items.Length);
            //Assert.AreEqual(array.Length, items.Length);
            for (int i = 0; i < array.Length; i++)
            {
                array[i].Should().Be(items[i]);
                //Assert.AreEqual(array[i], items[i]);
            }            
        }
    }
}