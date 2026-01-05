using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.ExtraLean
{
    public class PooledIteratorBlock<P>:IEnumerator where P : class, new()
    {
        IEnumerator iteratorBlock;
        P          data;
        IteratorBlockPool<P> pool;
        
        public PooledIteratorBlock( IEnumerator iEnumerator, P data, IteratorBlockPool<P> pool)
        { 
            iteratorBlock = iEnumerator;
            this.pool = pool;
            this.data = data;
        }

        public void Refresh(IEnumerator iEnumerator)
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
            if (canMove == false || iteratorBlock.Current is TaskContract.Break taskContractBreak && taskContractBreak.AnyBreak)
            {
                Release();
                return false;
            }

            return true;
        }
        public override string ToString()
        {
            return pool.name;
        }
        public void Reset() { }
        public object Current => iteratorBlock.Current;
    }
    public class IteratorBlockPool<P> where P : class, new()
    {
        readonly Stack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)> _pool = new Stack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)>();
        readonly Func<P, IEnumerator> _iteratorBlock;
        internal readonly string name;

        public IteratorBlockPool(Func<P, IEnumerator> iteratorBlock, string profilingName)
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