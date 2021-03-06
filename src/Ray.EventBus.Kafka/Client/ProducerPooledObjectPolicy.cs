﻿using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Ray.EventBus.Kafka
{
    public class ProducerPooledObjectPolicy : IPooledObjectPolicy<PooledProducer>
    {
        readonly ProducerConfig producerConfig;
        readonly ILogger logger;
        public ProducerPooledObjectPolicy(ProducerConfig producerConfig, ILogger logger)
        {
            this.producerConfig = producerConfig;
            this.producerConfig.EnableDeliveryReports = false;
            this.logger = logger;
        }
        public PooledProducer Create()
        {
            return new PooledProducer
            {
                Handler = new ProducerBuilder<string, byte[]>(producerConfig).SetErrorHandler(ConsumerClient_OnProducerError).Build()
            };
        }
        private void ConsumerClient_OnProducerError(IProducer<string, byte[]> consumer, Error e)
        {
            logger.LogCritical("An error occurred during connect kafka(producer) -->reason:{0},code:{1}", e.Reason, e.Code.ToString());
        }
        public bool Return(PooledProducer obj)
        {
            return true;
        }
    }
}
