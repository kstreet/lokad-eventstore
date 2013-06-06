#region (c) 2010-2013 Lokad EventStore - New BSD License 

// Copyright (c) Lokad 2010-2013 and contributors, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System.IO;
using NUnit.Framework;

namespace Lokad.EventStore.Tests.Cache.LockingInMemoryCache
{
    [TestFixture]
    public sealed class when_doing_concurrent_append : fixture_with_cache_helpers
    {
        [Test]
        public void given_empty_cache_and_valid_commit_function()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();

            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            cache.ConcurrentAppend("stream", GetEventBytes(1), (version, storeVersion) =>
                {
                    commitStoreVersion = storeVersion;
                    commitStreamVersion = version;
                });

            Assert.AreEqual(1, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(1, commitStreamVersion, "commitStreamVersion");

            Assert.AreEqual(1, cache.StoreVersion);

            var expected = new[]
                {
                    CreateKey(1, 1, "stream"),
                };
            DataAssert.AreEqual(expected, cache.ReadStream("stream", 0, 100));
            DataAssert.AreEqual(expected, cache.ReadAll(0, 100));
        }

        [Test]
        public void given_reloaded_cache_and_commit_function_that_fails()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();

            cache.LoadHistory(CreateFrames("stream", "otherStream"));

            Assert.Throws<FileNotFoundException>(
                                                 () =>
                                                     cache.ConcurrentAppend("stream", new byte[1],
                                                         (version, storeVersion) =>
                                                             { throw new FileNotFoundException(); }));

            Assert.AreEqual(2, cache.StoreVersion);
        }

        [Test]
        public void given_filled_cache_and_concurrent_append_with_non_specified_version_expectation()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();

            cache.LoadHistory(CreateFrames("stream", "otherStream"));

            cache.ConcurrentAppend("stream", GetEventBytes(3), (version, storeVersion) => { }, -1);

            Assert.AreEqual(3, cache.StoreVersion);
        }

        [Test]
        public void given_filled_cache_and_concurrent_append_with_valid_version_expectation()
        {
            // GIVEN
            var cache = new EventStore.Cache.LockingInMemoryCache();

            cache.LoadHistory(CreateFrames("stream", "otherStream"));
            cache.ConcurrentAppend("stream", GetEventBytes(4), (version, storeVersion) => { });

            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            // WHEN
            cache.ConcurrentAppend("stream", GetEventBytes(5), (version, storeVersion) =>
                {
                    commitStoreVersion = storeVersion;
                    commitStreamVersion = version;
                }, 2);


            // EXPECT
            Assert.AreEqual(4, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(3, commitStreamVersion, "commitStreamVersion");
            Assert.AreEqual(4, cache.StoreVersion);
        }


        [Test]
        public void given_reloaded_cache_and_concurrent_append_with_invalid_expected_version()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();

            cache.LoadHistory(CreateFrames("stream", "otherStream"));

            bool commitWasCalled = false;

            Assert.Throws<AppendOnlyStoreConcurrencyException>(
                                                               () =>
                                                                   cache.ConcurrentAppend("stream", new byte[1],
                                                                       (streamVersion, storeVersion) =>
                                                                           commitWasCalled = true, 2));

            Assert.IsFalse(commitWasCalled, "commit should not be called");
        }

        [Test]
        public void given_empty_cache_and_matching_version_expectation()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();
            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            cache.ConcurrentAppend("stream", new byte[1], (version, storeVersion) =>
                {
                    commitStoreVersion = 1;
                    commitStreamVersion = 1;
                }, 0);
            Assert.AreEqual(1, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(1, commitStreamVersion, "commitStreamVersion");
        }

        [Test]
        public void given_reloaded_cache_and_matching_stream_version()
        {
            var cache = new EventStore.Cache.LockingInMemoryCache();

            cache.LoadHistory(CreateFrames("stream", "otherStream"));

            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            cache.ConcurrentAppend("stream", new byte[1], (streamVersion, storeVersion) =>
                {
                    commitStoreVersion = storeVersion;
                    commitStreamVersion = streamVersion;
                }, 1);

            Assert.AreEqual(3, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(2, commitStreamVersion, "commitStreamVersion");
        }
    }
}