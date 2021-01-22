using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GentleTouch
{
// TODO (Chiv): Check and double check, license and Implementation.
// Currently, I am sick of searching for that so I just take it and move on.
//https://github.com/eiriktsarpalis/pq-tests/blob/master/PriorityQueue/PriorityQueue.cs

    public class PriorityQueueProposal<TElement, TPriority>
    {
        private const int DefaultCapacity = 4;

        private HeapEntry[] _heap;

        private UnorderedItemsCollection? _unorderedItemsCollection;
        private int _version;

        public int Count { get; private set; }

        public IComparer<TPriority> Comparer { get; }

        public UnorderedItemsCollection UnorderedItems =>
            _unorderedItemsCollection ??= new UnorderedItemsCollection(this);

        public void Enqueue(TElement element, TPriority priority)
        {
            _version++;
            if (Count == _heap.Length) Resize(ref _heap);

            SiftUp(Count++, in element, in priority);
        }

        public void EnqueueRange(IEnumerable<(TElement Element, TPriority Priority)> values)
        {
            _version++;
            if (Count == 0)
            {
                AppendRaw(values);
                Heapify();
            }
            else
            {
                foreach ((var element, var priority) in values)
                {
                    if (Count == _heap.Length) Resize(ref _heap);

                    SiftUp(Count++, in element, in priority);
                }
            }
        }

        // TODO optimize
        public void EnqueueRange(IEnumerable<TElement> elements, TPriority priority)
        {
            EnqueueRange(elements.Select(e => (e, priority)));
        }

        public TElement Peek()
        {
            if (Count == 0) throw new InvalidOperationException();

            return _heap[0].Element;
        }

        public bool TryPeek(out TElement element, out TPriority priority)
        {
            if (Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = _heap[0];
            return true;
        }

        public TElement Dequeue()
        {
            if (Count == 0) throw new InvalidOperationException();

            _version++;
            RemoveIndex(0, out var result, out _);
            return result;
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            _version++;
            RemoveIndex(0, out element, out priority);
            return true;
        }

        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (Count == 0) return element;

            ref var minEntry = ref _heap[0];
            if (Comparer.Compare(priority, minEntry.Priority) <= 0) return element;

            _version++;
            var minElement = minEntry.Element;
            SiftDown(0, in element, in priority);
            return minElement;
        }

        public void Clear()
        {
            _version++;
            if (Count > 0)
            {
                //if (RuntimeHelpers.IsReferenceOrContainsReferences<HeapEntry>())
                {
                    Array.Clear(_heap, 0, Count);
                }

                Count = 0;
            }
        }

        public void TrimExcess()
        {
            var count = Count;
            var threshold = (int) (_heap.Length * 0.9);
            if (count < threshold) Array.Resize(ref _heap, count);
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException();

            if (capacity > _heap.Length) Array.Resize(ref _heap, capacity);
        }

        public class UnorderedItemsCollection : IReadOnlyCollection<(TElement Element, TPriority Priority)>, ICollection
        {
            private readonly PriorityQueueProposal<TElement, TPriority> _priorityQueue;

            internal UnorderedItemsCollection(PriorityQueueProposal<TElement, TPriority> priorityQueue)
            {
                _priorityQueue = priorityQueue;
            }

            public bool IsSynchronized => false;
            public object SyncRoot => _priorityQueue;

            bool ICollection.IsSynchronized => false;
            object ICollection.SyncRoot => this;

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array));
                if (array.Rank != 1)
                    throw new ArgumentException("SR.Arg_RankMultiDimNotSupported", nameof(array));
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index), "SR.ArgumentOutOfRange_Index");

                var arrayLen = array.Length;
                if (arrayLen - index < _priorityQueue.Count)
                    throw new ArgumentException("SR.Argument_InvalidOffLen");

                var numToCopy = _priorityQueue.Count;
                HeapEntry[] heap = _priorityQueue._heap;

                for (var i = 0; i < numToCopy; i++)
                {
                    ref var entry = ref heap[i];
                    array.SetValue((entry.Element, entry.Priority), index + i);
                }
            }

            public int Count => _priorityQueue.Count;

            IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.
                GetEnumerator()
            {
                return new Enumerator(_priorityQueue);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(_priorityQueue);
            }

            public Enumerator GetEnumerator()
            {
                return new(_priorityQueue);
            }

            public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)>, IEnumerator
            {
                private readonly PriorityQueueProposal<TElement, TPriority> _queue;
                private readonly int _version;
                private int _index;

                internal Enumerator(PriorityQueueProposal<TElement, TPriority> queue)
                {
                    _version = queue._version;
                    _queue = queue;
                    _index = 0;
                    Current = default;
                }

                public bool MoveNext()
                {
                    PriorityQueueProposal<TElement, TPriority> queue = _queue;

                    if (queue._version == _version && _index < queue.Count)
                    {
                        ref HeapEntry entry = ref queue._heap[_index];
                        Current = (entry.Element, entry.Priority);
                        _index++;
                        return true;
                    }

                    if (queue._version != _version) throw new InvalidOperationException("collection was modified");

                    return false;
                }

                public (TElement Element, TPriority Priority) Current { get; private set; }

                object IEnumerator.Current => Current;

                public void Reset()
                {
                    if (_queue._version != _version) throw new InvalidOperationException("collection was modified");

                    _index = 0;
                    Current = default;
                }

                public void Dispose()
                {
                }
            }
        }

        #region Constructors

        public PriorityQueueProposal() : this(0, null)
        {
        }

        public PriorityQueueProposal(int initialCapacity) : this(initialCapacity, null)
        {
        }

        public PriorityQueueProposal(IComparer<TPriority>? comparer) : this(0, comparer)
        {
        }

        public PriorityQueueProposal(int initialCapacity, IComparer<TPriority>? comparer)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            if (initialCapacity == 0)
                _heap = Array.Empty<HeapEntry>();
            else
                _heap = new HeapEntry[initialCapacity];

            Comparer = comparer ?? Comparer<TPriority>.Default;
        }

        public PriorityQueueProposal(IEnumerable<(TElement Element, TPriority Priority)> values) : this(values, null)
        {
        }

        public PriorityQueueProposal(IEnumerable<(TElement Element, TPriority Priority)> values, IComparer<TPriority>? comparer)
        {
            Comparer = comparer ?? Comparer<TPriority>.Default;
            _heap = Array.Empty<HeapEntry>();
            Count = 0;

            AppendRaw(values);
            Heapify();
        }

        #endregion

        #region Private Methods

        private void Heapify()
        {
            HeapEntry[] heap = _heap;

            for (var i = (Count - 1) >> 2; i >= 0; i--)
            {
                var entry = heap[i]; // ensure struct is copied before sifting
                SiftDown(i, in entry.Element, in entry.Priority);
            }
        }

        private void AppendRaw(IEnumerable<(TElement Element, TPriority Priority)> values)
        {
            // TODO: specialize on ICollection types
            var heap = _heap;
            var count = Count;

            foreach ((var element, var priority) in values)
            {
                if (count == heap.Length) Resize(ref heap);

                ref var entry = ref heap[count];
                entry.Element = element;
                entry.Priority = priority;
                count++;
            }

            _heap = heap;
            Count = count;
        }

        private void RemoveIndex(int index, out TElement element, out TPriority priority)
        {
            Debug.Assert(index < Count);

            (element, priority) = _heap[index];

            var lastElementPos = --Count;
            ref var lastElement = ref _heap[lastElementPos];

            if (lastElementPos > 0) SiftDown(index, in lastElement.Element, in lastElement.Priority);

            //if (RuntimeHelpers.IsReferenceOrContainsReferences<HeapEntry>())
            {
                lastElement = default;
            }
        }

        private void SiftUp(int index, in TElement element, in TPriority priority)
        {
            while (index > 0)
            {
                var parentIndex = (index - 1) >> 2;
                ref var parent = ref _heap[parentIndex];

                if (Comparer.Compare(parent.Priority, priority) <= 0)
                    // parentPriority <= priority, heap property is satisfed
                    break;

                _heap[index] = parent;
                index = parentIndex;
            }

            ref var entry = ref _heap[index];
            entry.Element = element;
            entry.Priority = priority;
        }

        private void SiftDown(int index, in TElement element, in TPriority priority)
        {
            int minChildIndex;
            var count = Count;
            HeapEntry[] heap = _heap;

            while ((minChildIndex = (index << 2) + 1) < count)
            {
                // find the child with the minimal priority
                ref var minChild = ref heap[minChildIndex];
                var childUpperBound = Math.Min(count, minChildIndex + 4);

                for (var nextChildIndex = minChildIndex + 1; nextChildIndex < childUpperBound; nextChildIndex++)
                {
                    ref var nextChild = ref heap[nextChildIndex];
                    if (Comparer.Compare(nextChild.Priority, minChild.Priority) < 0)
                    {
                        minChildIndex = nextChildIndex;
                        minChild = ref nextChild;
                    }
                }

                // compare with inserted priority
                if (Comparer.Compare(priority, minChild.Priority) <= 0)
                    // priority <= minChild, heap property is satisfied
                    break;

                heap[index] = minChild;
                index = minChildIndex;
            }

            ref var entry = ref heap[index];
            entry.Element = element;
            entry.Priority = priority;
        }

        private void Resize(ref HeapEntry[] heap)
        {
            var newSize = heap.Length == 0 ? DefaultCapacity : 2 * heap.Length;
            Array.Resize(ref heap, newSize);
        }

        private struct HeapEntry
        {
            public TElement Element;
            public TPriority Priority;

            public void Deconstruct(out TElement element, out TPriority priority)
            {
                element = Element;
                priority = Priority;
            }
        }

#if DEBUG
        public void ValidateInternalState()
        {
            if (_heap.Length < Count) throw new Exception("invalid elements array length");

            foreach (var (element, idx) in _heap.Select((x, i) => (x.Element, i)).Skip(Count))
                if (!IsDefault(element))
                    throw new Exception($"Non-zero element '{element}' at index {idx}.");

            foreach (var (priority, idx) in _heap.Select((x, i) => (x.Priority, i)).Skip(Count))
                if (!IsDefault(priority))
                    throw new Exception($"Non-zero priority '{priority}' at index {idx}.");

            static bool IsDefault<T>(T value)
            {
                T defaultVal = default;

                if (defaultVal is null) return value is null;

                return value!.Equals(defaultVal);
            }
        }
#endif

        #endregion
    }
}