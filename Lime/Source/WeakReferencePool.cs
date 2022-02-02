using System;
using System.Collections.Generic;
using System.Threading;

namespace Lime
{
	public class WeakReferencePool<TKey, TValue> where TValue : class
	{
		private const int ConcurrencyLevel = 37;

		public delegate TValue CreateItemDelegate(TKey key);

		private readonly Store[] stores;
		private readonly IEqualityComparer<TKey> comparer;

		public WeakReferencePool(CreateItemDelegate createItem)
			: this(createItem, EqualityComparer<TKey>.Default, false) { }

		public WeakReferencePool(CreateItemDelegate createItem, IEqualityComparer<TKey> comparer, bool trackResurrection)
		{
			this.comparer = comparer;
			stores = new Store[ConcurrencyLevel];
			for (var i = 0; i < stores.Length; i++) {
				stores[i] = new Store(createItem, new PrehashedKeyComparer(comparer), trackResurrection);
			}
		}

		public TValue GetItem(TKey key)
		{
			var (prehashedKey, index) = GetPrehashedKeyAndStoreIndex(key);
			return stores[index].GetItem(prehashedKey);
		}

		public bool TryRemoveItem(TKey key)
		{
			var (prehashedKey, index) = GetPrehashedKeyAndStoreIndex(key);
			return stores[index].TryRemoveItem(prehashedKey);
		}

		private (PrehashedKey, int) GetPrehashedKeyAndStoreIndex(TKey key)
		{
			var prehashedKey = new PrehashedKey(comparer.GetHashCode(key), key);
			var index = (int)(unchecked((uint)prehashedKey.Hash) % stores.Length);
			return (prehashedKey, index);
		}
		
		private class Store
		{
			private const int MinCountUntilCleanUp = 4;

			private readonly CreateItemDelegate createItem;
			private readonly bool trackResurrection;
			private readonly Dictionary<PrehashedKey, CacheEntry> cache;
			private readonly object cacheLock = new object();
			private int countUntilCleanUp = MinCountUntilCleanUp;

			public Store(CreateItemDelegate createItem, IEqualityComparer<PrehashedKey> comparer, bool trackResurrection)
			{
				cache = new Dictionary<PrehashedKey, CacheEntry>(comparer);
				this.trackResurrection = trackResurrection;
				this.createItem = createItem;
			}

			public TValue GetItem(PrehashedKey prehashedKey)
			{
				CacheEntry entry;
				lock (cacheLock) {
					if (!cache.TryGetValue(prehashedKey, out entry)) {
						if (cache.Count >= countUntilCleanUp) {
							CleanUpGarbageCollectedObjects();
						}
						entry = new CacheEntry();
						cache.Add(prehashedKey, entry);
					}
					Interlocked.Increment(ref entry.LockCount);
				}
				TValue result;
				try {
					lock (entry) {
						if (entry.Reference == null || !entry.Reference.TryGetTarget(out result)) {
							result = createItem(prehashedKey.Key);
							entry.Reference = new WeakReference<TValue>(result, trackResurrection);
						}
					}
				} finally {
					Interlocked.Decrement(ref entry.LockCount);
				}
				return result;
			}

			public bool TryRemoveItem(PrehashedKey prehashedKey)
			{
				CacheEntry entry;
				lock (cacheLock) {
					if (!cache.TryGetValue(prehashedKey, out entry)) {
						return false;
					}
					Interlocked.Increment(ref entry.LockCount);
				}
				bool isReferenceRemoved = false;
				try {
					lock (entry) {
						if (entry.Reference != null && entry.Reference.TryGetTarget(out _)) {
							entry.Reference = null;
							isReferenceRemoved = true;
						}
					}
				} finally {
					Interlocked.Decrement(ref entry.LockCount);
				}
				return isReferenceRemoved;
			}

			private void CleanUpGarbageCollectedObjects()
			{
				var toRemove = new List<PrehashedKey>();
				foreach (var (key, entry) in cache) {
					bool locked = Interlocked.CompareExchange(ref entry.LockCount, 0, 0) != 0;
					if (!locked && (entry.Reference == null || !entry.Reference.TryGetTarget(out _))) {
						toRemove.Add(key);
					}
				}
				if (toRemove.Count > 0) {
					foreach (var key in toRemove) {
						cache.Remove(key);
					}
				} else {
					countUntilCleanUp *= 2;
				}
			}

			private class CacheEntry
			{
				public int LockCount;
				public WeakReference<TValue> Reference;
			}
		}

		private struct PrehashedKey
		{
			public readonly int Hash;
			public readonly TKey Key;

			public PrehashedKey(int hash, TKey key)
			{
				Hash = hash;
				Key = key;
			}
		}

		private class PrehashedKeyComparer : IEqualityComparer<PrehashedKey>
		{
			private readonly IEqualityComparer<TKey> baseComparer;

			public PrehashedKeyComparer(IEqualityComparer<TKey> baseComparer)
			{
				this.baseComparer = baseComparer;
			}

			public bool Equals(PrehashedKey x, PrehashedKey y) => baseComparer.Equals(x.Key, y.Key);

			public int GetHashCode(PrehashedKey x) => x.Hash;
		}
	}
}
