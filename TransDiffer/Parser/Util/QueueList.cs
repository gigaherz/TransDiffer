using System;
using System.Collections;
using System.Collections.Generic;

namespace TransDiffer.Parser.Util
{
    public class QueueList<T> : IEnumerable<T>
    {
        private const int DefaultCapacity = 16;

        private int capacityMask;
        private int start;
        private T[] buffer;

        public int Count { get; private set; }

        public T this[int index]
        {
            get
            {
                if (index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var result = buffer[ToBufferIndex(index)];

                return result;
            }
        }

        public QueueList()
            : this(DefaultCapacity)
        {
        }

        public QueueList(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "capacity is less than 0.");
            }

            GrowTo(capacity);
        }

        // Ooooh... I just got how this works! Clever!
        // It's causing all the bits to spread downward
        // until all the bits below the most-significant 1
        // are also 1, then adds 1 to fill the power of two.
        private static int UpperPower(int x)
        {
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        private void GrowTo(int value)
        {
            value = UpperPower(value);
            if (null != buffer && value == buffer.Length)
                return;

            var newBuffer = new T[value];
            for (int i = 0; i < Count; i++)
            {
                newBuffer[i] = this[i];
            }

            buffer = newBuffer;
            start = 0;
            capacityMask = value - 1;
        }

        private void EnsureCapacityFor(int numElements)
        {
            if (Count + numElements > buffer.Length)
            {
                GrowTo(Count + numElements);
            }
        }

        private int ToBufferIndex(int index)
        {
            return (index + start) & capacityMask;
        }

        public void Add(T item)
        {
            EnsureCapacityFor(1);
            buffer[ToBufferIndex(Count)] = item;
            Count++;
        }

        public T Remove()
        {
            if (Count == 0)
                throw new InvalidOperationException("The Deque is empty");

            var result = buffer[start];

            buffer[start] = default(T);
            start = ToBufferIndex(1);
            Count--;
            return result;
        }

        // VERY unsafe iterator!
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
