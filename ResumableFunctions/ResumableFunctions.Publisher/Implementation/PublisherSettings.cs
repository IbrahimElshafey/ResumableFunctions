using EnsureThat;
using ResumableFunctions.Publisher.Abstraction;
using System;
using System.Collections.Generic;

namespace ResumableFunctions.Publisher.Implementation
{
    public class PublisherSettings : IPublisherSettings
    {

        public PublisherSettings(Dictionary<string, string> servicesRegistry, TimeSpan checkFailedRequestEvery = default)
        {
            Ensure.That(servicesRegistry).IsNotNull();
            Ensure.That(servicesRegistry).HasItems();
            ServicesRegistry = servicesRegistry;
            if (checkFailedRequestEvery != default)
                CheckFailedRequestEvery = checkFailedRequestEvery;
        }

        //todo: convert this to method that is generic
        //also IFailedRequestHandler, IFailedRequestRepo
        public Type CallPublisherType => typeof(HttpCallPublisher);

        public Dictionary<string, string> ServicesRegistry { get; }

        public TimeSpan CheckFailedRequestEvery { get; } = TimeSpan.FromMinutes(30);
    }
}
