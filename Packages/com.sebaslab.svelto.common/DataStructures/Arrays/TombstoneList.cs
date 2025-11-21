#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures.Experimental
{
    public struct FlaggedItem<T>
    {
        public T Item;
        public uint NextUnusedIndex; // -1 indicates the cell is used, otherwise points to the next unused cell
    }

    /// <summary>
    /// DO NOT USE, STILL WORKING ON IT, DOESN't WORK
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

        public TombstoneList(uint initialSize)
        {
            _count = 0;
            _firstUnusedIndex = 1;

            _buffer = new FlaggedItem<T>[initialSize];
        }

        public TombstoneList(int initialSize) : this((uint)initialSize)
        {
        }

        public int count    => (int)_count;
        public int capacity => _buffer.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGet(int index)
        {
#if ENABLE_DEBUG_CHECKS
                if ((uint)index > _largestUsedIndex)
                    throw new Exception($"TombstoneList - out of bound access: index {index} - count {_largestUsedIndex}");
#endif                
            return ref _buffer[(uint)index].Item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGet(uint index)
        {
#if ENABLE_DEBUG_CHECKS
                if (index > _largestUsedIndex)
                    throw new Exception($"TombstoneList - out of bound access: index {index} - count {_largestUsedIndex}");
#endif                
            return ref _buffer[index].Item;
        }

        // -------------------------------------------------------------------------
        // shared helper – returns the index of the slot that has just been taken
        // -------------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        uint TakeFreeSlot()
        {
            AllocateMore();

            uint indexToUse = _firstUnusedIndex - 1;  // convert base-1 → base-0
            _count++;

            uint nextUnused = _buffer[indexToUse].NextUnusedIndex;
            _firstUnusedIndex = nextUnused > 0       // 0 means "already used"
                ? nextUnused
                : _count + 1;                        // list exhausted – point past end

            _buffer[indexToUse].NextUnusedIndex = 0; // mark slot as used

#if ENABLE_DEBUG_CHECKS
            if (indexToUse > _largestUsedIndex)
                _largestUsedIndex = indexToUse;
#endif
            return indexToUse;
        }

        // -------------------------------------------------------------------------
        // public API
        // -------------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Add(in T item)
        {
            uint index = TakeFreeSlot();
            _buffer[index].Item = item;
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AddByRef()
        {
            uint index = TakeFreeSlot();
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            return ref _buffer[index].Item;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(uint index)
        {
            //Check if the index is within the buffer bounds
            DBC.Common.Check.Require(index                          < _buffer.Length, $"out of bound index {index} - count {_buffer.Length}");
            //Check if the index is not negative (although uint makes this check redundant)
            DBC.Common.Check.Require(index                          >= 0, "index must be greater than 0");
            //Check if the item at this index is currently in use (NextUnusedIndex == 0 means the slot is used)
            DBC.Common.Check.Require(_buffer[index].NextUnusedIndex == 0, $"trying to access a tombstone at index {index} that is already removed");
            
            //Link this newly freed slot to the previous first unused slot
            _buffer[index].NextUnusedIndex = _firstUnusedIndex;
            _buffer[index].Item = default; //clear the item as it could hold references to other objects
            //Make this slot the new first unused slot (add 1 because indices are stored in base 1)
            _firstUnusedIndex = index + 1; //updating linked list and first empty slot in base 1
            //Decrease the total count of used slots
            _count--;
            
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            RemoveAt((uint)index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TombstoneListEnumerator<T> GetEnumerator()
        {
            return new TombstoneListEnumerator<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateMore()
        {
            if (_count == _buffer.Length)
            {
                var newLength = (int)((_buffer.Length + 1) * 1.5f);
                FlaggedItem<T>[] newList   = new FlaggedItem<T>[newLength];
                Array.Copy(_buffer, newList, _count);
                _buffer = newList;
            }
        }
        
        public void Clear()
        {
            _count = 0;
            _firstUnusedIndex = 1;

            if (TypeCache<T>.isUnmanaged == false) 
                Array.Clear(_buffer, 0, _buffer.Length);
        }

        internal FlaggedItem<T>[]  _buffer;
        uint _firstUnusedIndex;
        uint _count;
        
#if ENABLE_DEBUG_CHECKS
        uint _largestUsedIndex;
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