using System;

namespace GPeerToPeer
{
    public class TempList<T>
    {
        private Dictionary<DateTime, T> values;
        private object valuesLock;
        private TimeSpan ttl { get; set; }
        public TempList(long seconds)
        {
            values = new Dictionary<DateTime, T>();
            valuesLock = new object();
            ttl = TimeSpan.FromSeconds(seconds);
        }
        private void DeleteOldValues()
        {
            foreach (DateTime time in values.Keys)
            {
                if (time + ttl < DateTime.UtcNow) values.Remove(time);
            }
        }
        public void Add(T value)
        {
            lock (valuesLock)
            {
                DeleteOldValues();
                values.Add(DateTime.UtcNow, value);
            }
        }
        public bool Get(ref T value)
        {
            Task<T> t = Task.Run(() =>
            {
                while (true)
                {
                    lock (valuesLock)
                    {
                        DeleteOldValues();
                        if (values.Count > 0)
                        {
                            DateTime minDateTime = values.Min(x => x.Key);
                            T retValue = values[minDateTime];
                            values.Remove(minDateTime);
                            return retValue;
                        }
                    }
                }
            });
            if (!t.Wait(ttl))
            {
                return false;
            }
            value = t.GetAwaiter().GetResult();
            return true;
        }
        public bool Contains(T value)
        {
            lock (valuesLock)
            {
                DeleteOldValues();
                foreach (T value1 in values.Values)
                {
                    if (value1.Equals(value)) return true;
                }
                return false;
            }
        }
    }
}
