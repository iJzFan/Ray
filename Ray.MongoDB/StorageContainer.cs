﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Orleans;
using Ray.Core.Abstractions;

namespace Ray.Storage.MongoDB
{
    public class StorageContainer : IStorageContainer, IConfigContainer
    {
        readonly IServiceProvider serviceProvider;
        readonly ConcurrentDictionary<Type, object> configBuilderDict = new ConcurrentDictionary<Type, object>();
        readonly ConcurrentDictionary<string, ValueTask<MongoGrainConfig>> grainConfigDict = new ConcurrentDictionary<string, ValueTask<MongoGrainConfig>>();
        public StorageContainer(
            IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
        readonly ConcurrentDictionary<string, object> eventStorageDict = new ConcurrentDictionary<string, object>();
        public async ValueTask<IEventStorage<K>> GetEventStorage<K, S>(Grain grain, K grainId)
             where S : class, IState<K>, new()
        {
            var grainType = grain.GetType();
            if (configBuilderDict.TryGetValue(grainType, out var value) &&
                value is GrainConfigBuilderWrapper<K> builder)
            {
                var dictKey = builder.IgnoreGrainId ? grainType.FullName : $"{grainType.FullName}-{grainId.ToString()}";
                var configTask = grainConfigDict.GetOrAdd(dictKey, async key =>
                {
                    var newConfig = builder.Generator(grain, grainId);
                    if (!string.IsNullOrEmpty(builder.SnapshotCollection))
                        newConfig.SnapshotCollection = builder.SnapshotCollection;
                    var task = newConfig.Build();
                    if (!task.IsCompleted)
                        await task;
                    return newConfig;
                });
                if (!configTask.IsCompleted)
                    await configTask;
                var storage = eventStorageDict.GetOrAdd(dictKey, key =>
                 {
                     return new MongoEventStorage<K>(serviceProvider, configTask.Result);
                 });
                return storage as MongoEventStorage<K>;
            }
            else
            {
                throw new NotImplementedException($"{nameof(GrainConfigBuilderWrapper<K>)} of {grainType.FullName}");
            }
        }
        readonly ConcurrentDictionary<string, object> stateStorageDict = new ConcurrentDictionary<string, object>();
        public async ValueTask<IStateStorage<S, K>> GetStateStorage<K, S>(Grain grain, K grainId)
            where S : class, IState<K>, new()
        {
            var grainType = grain.GetType();
            if (configBuilderDict.TryGetValue(grainType, out var value) &&
                value is GrainConfigBuilderWrapper<K> builder)
            {
                var dictKey = builder.IgnoreGrainId ? grainType.FullName : $"{grainType.FullName}-{grainId.ToString()}";
                var configTask = grainConfigDict.GetOrAdd(dictKey, async key =>
                {
                    var newConfig = builder.Generator(grain, grainId);
                    if (!string.IsNullOrEmpty(builder.SnapshotCollection))
                        newConfig.SnapshotCollection = builder.SnapshotCollection;
                    var task = newConfig.Build();
                    if (!task.IsCompleted)
                        await task;
                    return newConfig;
                });
                if (!configTask.IsCompleted)
                    await configTask;
                var storage = stateStorageDict.GetOrAdd(dictKey, key =>
               {
                   return new MongoStateStorage<S, K>(configTask.Result);
               });
                return storage as MongoStateStorage<S, K>;
            }
            else
            {
                throw new NotImplementedException($"{nameof(GrainConfigBuilderWrapper<K>)} of {grainType.FullName}");
            }
        }

        public GrainConfigBuilder<K> CreateBuilder<K>(Func<Grain, K, MongoGrainConfig> generator, bool ignoreGrainId = true)
        {
            return new GrainConfigBuilder<K>(this, generator, ignoreGrainId);
        }

        public void RegisterBuilder<K>(Type type, GrainConfigBuilderWrapper<K> builder)
        {
            configBuilderDict.TryAdd(type, builder);
        }
    }
}