﻿using System.Collections.Generic;

namespace xasset
{
    public class RequestQueue
    {
        private readonly List<Request> processing = new List<Request>();
        private readonly Queue<Request> queue = new Queue<Request>();
        public string key;
        public byte maxRequests { get; set; } = 10;
        public bool working => processing.Count > 0 || queue.Count > 0;
        public int priority { get; set; }

        public void Enqueue(Request request)
        {
            queue.Enqueue(request);
        }

        public bool Update()
        {
            while (queue.Count > 0 && (processing.Count < maxRequests || maxRequests == 0))
            {
                var item = queue.Dequeue();
                processing.Add(item);
                if (item.status == Request.Status.Wait) item.Start();
                if (Scheduler.Busy) return false;
            }

            for (var index = 0; index < processing.Count; index++)
            {
                var item = processing[index];
                if (item.Update()) continue;
                processing.RemoveAt(index);
                index--;
                item.Complete();
                if (Scheduler.Busy) return false;
            }

            return true;
        }
    }
}