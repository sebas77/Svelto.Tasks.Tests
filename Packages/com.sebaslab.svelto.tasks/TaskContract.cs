using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks
{
    public struct TaskContract
    {
        public  TaskContract(int number) : this()
        {
            _currentState      = States.value;
            _returnValue.int32 = number;
        }

        public  TaskContract(ulong number) : this()
        {
            _currentState       = States.value;
            _returnValue.uint64 = number;
        }
        
        public TaskContract(float val) : this()
        {
            _currentState       = States.value;
            _returnValue.single = val;
        }
        
        public TaskContract(uint val) : this()
        {
            _currentState       = States.value;
            _returnValue.uint32 = val;
        }
        
        public TaskContract(string val) : this()
        {
            _currentState            = States.value;
            _returnObjects.reference = val;
        }
        
        public TaskContract(object o) : this()
        {
            _currentState            = States.reference;
            _returnObjects.reference = o;
        }

        internal TaskContract(Continuation continuation) : this()
        {
            _currentState = States.continuation;
            _continuation = continuation;
        }

        internal TaskContract(IEnumerator<TaskContract> enumerator) : this()
        {
            _currentState            = States.leanEnumerator;
            _returnObjects.reference = enumerator;
        }
        
        internal TaskContract(IEnumerator enumerator) : this()
        {
            _currentState            = States.extraLeanEnumerator;
            _returnObjects.reference = enumerator;
        }

        internal static TaskContract FromEnumerator(IEnumerator enumerator)
        {
            return new TaskContract(enumerator, States.extraLeanEnumerator);
        }
        
        internal static TaskContract FromContractEnumerator(IEnumerator enumerator)
        {
            return new TaskContract(enumerator, States.leanEnumerator);
        }
        
        TaskContract(IEnumerator enumerator, States state) : this()
        {
            _currentState            = state;
            _returnObjects.reference = enumerator;
        }

        TaskContract(Break breakit) : this()
        {
            _currentState          = States.breakit;
            _returnObjects.breakIt = breakit;
        }

        TaskContract(Yield yieldIt) : this()
        {
            _currentState = States.yieldit;
        }

        public static implicit operator TaskContract(int number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(ulong number)
        {
            return new TaskContract(number);
        }
        
        public static implicit operator TaskContract(long number)
        {
            return new TaskContract(number);
        }
        
        public static implicit operator TaskContract(float number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(Continuation continuation)
        {
            return new TaskContract(continuation);
        }

        public static implicit operator TaskContract(Break breakit)
        {
            return new TaskContract(breakit);
        }

        public static implicit operator TaskContract(Yield yieldit)
        {
            return new TaskContract(yieldit);
        }
        
        public static implicit operator TaskContract(string payload)
        {
            return new TaskContract(payload);
        }
        
        public int   ToInt()                    => _returnValue.int32;
        public ulong ToUlong()                  => _returnValue.uint64;
        public long  ToLong()                   => (long)_returnValue.uint64;
        public uint  ToUInt()                   => _returnValue.uint32;
        public float ToFloat()                  => _returnValue.single;
        public T     ToRef<T>() where T : class => _returnObjects.reference as T;

        internal Break breakIt => _currentState == States.breakit ? _returnObjects.breakIt : null;

        internal IEnumerator enumerator => _currentState == States.leanEnumerator || 
            _currentState == States.extraLeanEnumerator ? (IEnumerator) _returnObjects.reference : null;

        internal Continuation? Continuation
        {
            get
            {
                if (_currentState != States.continuation)
                    return null;

                return _continuation;
            }
        }
        
        internal bool isTaskEnumerator => _currentState == States.leanEnumerator;
        internal object reference => _currentState == States.value ? _returnObjects.reference : null;
        internal bool hasValue => _currentState == States.value;
        internal bool yieldIt => _currentState == States.yieldit;
        
        readonly FieldValues  _returnValue;
        readonly FieldObjects _returnObjects;
        readonly States       _currentState;
        readonly Continuation _continuation;
        
        [StructLayout(LayoutKind.Explicit)]
        struct FieldValues
        {
            [FieldOffset(0)] internal float single;
            [FieldOffset(0)] internal int   int32;
            [FieldOffset(0)] internal uint  uint32;
            [FieldOffset(0)] internal ulong uint64;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FieldObjects
        {
            [FieldOffset(0)] internal object reference;
            [FieldOffset(0)] internal Break  breakIt;
        }

        enum States
        {
            yieldit = 0,
            value,
            continuation,
            breakit,
            leanEnumerator,
            extraLeanEnumerator,
            reference
        }
    }
}