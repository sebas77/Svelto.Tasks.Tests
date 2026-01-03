using System;
using System.Collections.Generic;
using Svelto.Common;

namespace Svelto.Tasks.Lean
{
    public class PooledIteratorBlock<P>:IEnumerator<TaskContract> where P : class, new()
    {
        IEnumerator<TaskContract> iteratorBlock;
        P          data;
        IteratorBlockPool<P> pool;

        public PooledIteratorBlock( IEnumerator<TaskContract> iEnumerator, P data, IteratorBlockPool<P> pool)
        { 
            iteratorBlock = iEnumerator;
            this.pool = pool;
            this.data = data;
        }

        public void Refresh(IEnumerator<TaskContract> iEnumerator)
        {
            iteratorBlock = iEnumerator;
        }

        public void Release()
        {
            pool.Release(data, this);
        }
        
        public bool MoveNext()
        {
            var canMove = iteratorBlock.MoveNext();
            if (canMove == false || iteratorBlock.Current is TaskContract taskContract && taskContract.breakMode != null
             && taskContract.breakMode.AnyBreak)
            {
                Release();
                return false;
            }

            return true;
        }
        public void Reset() => throw new NotImplementedException();
        public object Current => iteratorBlock.Current;

        TaskContract IEnumerator<TaskContract>.Current => iteratorBlock.Current;
        
        public override string ToString()
        {
            return pool.name;
        }

        public void Dispose()
        {
            iteratorBlock?.Dispose();
        }
    }
    public class IteratorBlockPool<P> where P : class, new()
    {
        readonly Stack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)> _pool = new Stack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)>();
        readonly Func<P, IEnumerator<TaskContract> > _iteratorBlock;
        internal readonly string name;

        public IteratorBlockPool(Func<P, IEnumerator<TaskContract>> iteratorBlock, string profilingName)
        {
            _iteratorBlock = iteratorBlock;
            name = profilingName;
        }

        public (P data, PooledIteratorBlock<P> pooledIteratorBlock) Get()
        {
            if (_pool.Count == 0)
            {
                var data = new P();

                return (data, new PooledIteratorBlock<P>(_iteratorBlock(data), data, this));
            }

            var result = _pool.Pop();
            result.pooledIteratorBlock.Refresh(_iteratorBlock(result.data));
            return result;
        }

        public void Release(P data, PooledIteratorBlock<P> pooledIteratorBlock)
        {
            _pool.Push((data, pooledIteratorBlock));
        }
    }
}