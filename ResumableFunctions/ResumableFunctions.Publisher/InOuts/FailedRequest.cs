using System;

namespace ResumableFunctions.Publisher.InOuts
{
    public class FailedRequest
    {
        public Guid Key { get; }

        public FailedRequest(Guid key, DateTime created, string actionUrl, byte[] body)
        {
            Key = key;
            Created = created;
            ActionUrl = actionUrl;
            Body = body;
        }

        public string ActionUrl { get; }
        public byte[] Body { get; }
        public DateTime Created { get; }
        public int AttemptsCount { get; set; } = 1;
        public DateTime LastAttemptDate { get; set; }
    }
}