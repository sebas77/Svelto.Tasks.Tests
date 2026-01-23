
#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public struct FlaggedItem<T>
    {
        public T Item;
        public int NextUnusedIndex; // 0 indicates the cell is used, otherwise points to the next unused cell (+1)
    }
    
    public struct TombstoneHandle
    {
        public static readonly TombstoneHandle Invalid = new TombstoneHandle(-1);

        public TombstoneHandle(int index)
        {
            this.index = index;
        }
        
        public static explicit operator int(TombstoneHandle handle) => (int)handle.index;

        public readonly int index;
    }

    /// <summary>
    /// A list that allows for O(1) removal by leaving a tombstone in the removed slot.
    /// The removed slots are reused for future additions.
    /// I needed this data structures to keep stable buffer indices of existing items while allowing removals 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TombstoneList<T>
    {
        public TombstoneList()
        {
            _count = 0;
            _firstUnusedIndex = 1;

            _buffer = Array.Empty<FlaggedItem<T>>();
        }

        public TombstoneList(uint initialSize) : this((int)initialSize)
        {
         
        }

        public TombstoneList(int initialSize)
        {
            _count = 0;
            _firstUnusedIndex = 1;

            _buffer  = new FlaggedItem<T>[initialSize];
        }

        //count in this class is extremely tricky because it represents the number of used slots
        //not the number of total slots allocated
        public int count    => (int)_count;
        public int capacity => _buffer.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGet(int index)
        {
            DBC.Common.Check.Require(index                          >= 0, "index must be not negative");

            return ref UnsafeGet((uint)index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGet(uint index)
        {
            DBC.Common.Check.Require(index                          < _largestUsedCount, $"out of bound index {index} - largest used index {_largestUsedCount}");               
            DBC.Common.Check.Require(_buffer[index].NextUnusedIndex == 0, $"trying to access a tombstone at index {index} that is already removed");
            return ref _buffer[index].Item;
        }
        
        //To better visualize how _firstUnusedIndex and NextUnusedIndex work, is best to start from RemoveAt
        //once a slot is removed, its NextUnusedIndex points to the previous first unused slot and 
        //then _firstUnusedIndex is updated to point to this newly freed slot (index + 1 because it's in base 1)
        //effectively creating a linked list of unused slots
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(TombstoneHandle handle)
        {
            int index = handle.index;
            //Check if the index is within the buffer bounds
            DBC.Common.Check.Require(index                          < _largestUsedCount, $"out of bound index {index} - largest used index {_largestUsedCount}");
            //Check if the index is not negative (although uint makes this check redundant)
            DBC.Common.Check.Require(index                          >= 0, "index must be not negative");
            //Check if the item at this index is currently in use (NextUnusedIndex == 0 means the slot is used)
            ref var flaggedItem = ref _buffer[index];
            DBC.Common.Check.Require(flaggedItem.NextUnusedIndex == 0, $"trying to access a tombstone at index {index} that is already removed");

            flaggedItem.Item = default; //clear the item as it could hold references to other objects
            //Link this newly freed slot to the previous first unused slot
            
            //Make this slot the new first unused slot (add 1 because indices are stored in base 1)
            int nextUnusedIndex = Exchange(ref _firstUnusedIndex, index + 1); //updating linked list and first empty slot in base 1
            flaggedItem.NextUnusedIndex = nextUnusedIndex;
            
            //Decrease the total count of used slots
            Decrement(ref _count);

#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Decrement(ref int value) {
            value--;
        }

        //then if we want to add a new item, we check if there are any unused slots from the linked list
        //if there are, we take the first one pointed by _firstUnusedIndex (base 1, so we convert to base 0)
        //which represent the last removed slot. (the top of the linked list points to the last removed slot)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        TombstoneHandle TakeFreeSlot()
        {
            //if _firstUnusedIndex (base 1) is beyond the buffer length, we need to grow the buffer
            if (_firstUnusedIndex > _buffer.Length)
                AllocateMore((int)((_buffer.Length + 1) * 1.5f));
            
            int indexToUse = _firstUnusedIndex - 1;  // convert base-1 → base-0

            // this slot is not part of the free list anymore, setting to 0 can be used to check if the slot is used or not 
            // we use 0 as "used" flag inside the TombstoneListEnumerator
            var nextUnused = Exchange(ref _buffer[indexToUse].NextUnusedIndex, 0);

            if (nextUnused > 0) //the linked list is pointing to another unused slot
                _firstUnusedIndex = nextUnused; //take note of the next unused slot
            else
            {
                DBC.Common.Check.Require(_largestUsedCount == count, "inconsistent state in TombstoneList");
                //no slots to reuse available, we must use the first never used slot
                _firstUnusedIndex = _largestUsedCount + 1; //base-1
            }

            if (indexToUse >= _largestUsedCount)
                _largestUsedCount = indexToUse + 1;
            
            // count = 0 first unused = 1 (base-1, starting condition)
            // add first element => count = 1 first unused = 2 (base-1)
            _count++;

            return new TombstoneHandle(indexToUse);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Exchange(ref int nextUnusedIndex, int i)
        {
            var old = nextUnusedIndex;
            nextUnusedIndex = i;
            return old;
        }

        // -------------------------------------------------------------------------
        // public API
        // -------------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TombstoneHandle Add(in T item)
        {
            TombstoneHandle index = TakeFreeSlot();
            _buffer[(int)index].Item = item;
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AddByRef(out TombstoneHandle handle)
        {
            TombstoneHandle index = TakeFreeSlot();
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            handle = index;
            return ref _buffer[(int)index].Item;
        }
  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TombstoneListEnumerator<T> GetEnumerator()
        {
            return new TombstoneListEnumerator<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateMore(int newLength)
        {
            FlaggedItem<T>[] newList   = new FlaggedItem<T>[newLength];
               int oldLength = _buffer.Length;
                Array.Copy(_buffer, newList, oldLength);
         
            _buffer = newList;
        }
        
        public void Clear()
        {
            _count = 0;
            _firstUnusedIndex = 1;
            _largestUsedCount = 0;

            Array.Clear(_buffer, 0, _buffer.Length); //must reset the NextUnusedIndex flags too
        }

        internal FlaggedItem<T>[]  _buffer;
        int _firstUnusedIndex;
        int _count;
        int _largestUsedCount;
        
#if ENABLE_DEBUG_CHECKS
        
         internal int _version;
#endif  
       
    }

    //todo: to be tested
    public ref struct TombstoneListEnumerator<T>
    {
        internal TombstoneListEnumerator(TombstoneList<T> owner)
        {
            _owner            = owner;
#if ENABLE_DEBUG_CHECKS
            _capturedVersion  = owner._version;
#endif
            _index            = -1;
            _returned         = 0;
        }

        public ref T Current => ref _owner._buffer[_index].Item;
        public uint CurrentIndex => (uint)_index;

        public bool MoveNext()
        {
#if ENABLE_DEBUG_CHECKS
        if (_owner._version != _capturedVersion)
            throw new InvalidOperationException("Collection was modified during enumeration");
#endif
        // advance to next used slot
        while (++_index < _owner._buffer.Length)
        {
            if (_owner._buffer[_index].NextUnusedIndex == 0) // live element
            {
                if (++_returned > _owner.count)             // safety net
                    return false;

                return true;
            }
        }
        return false; // end of buffer
    }

    public void Reset()
    {
        _index    = -1;
        _returned = 0;
    }

    readonly TombstoneList<T> _owner;   // gives us live access to version & data
#if ENABLE_DEBUG_CHECKS
    readonly int              _capturedVersion;
#endif
    int                       _index;
    uint                      _returned;
}
}