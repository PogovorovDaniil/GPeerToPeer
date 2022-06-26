using System;

namespace GPeerToPeer
{
    public class TempList<T>
    {
        private Dictionary<DateTime, T> values;
        private TimeSpan ttl { get; set; }
        public TempList(long seconds)
        {
            values = new Dictionary<DateTime, T>();
            ttl = TimeSpan.FromSeconds(seconds);
        }

        public void Add(T value)
        {
            foreach (DateTime time in values.Keys)
            {
                if (time + ttl > DateTime.UtcNow) values.Remove(time);
            }
            values.Add(DateTime.UtcNow, value);
        }
        public bool Get(ref T value) 
        {
            if (values.Count == 0) return false;
            DateTime minDateTime = values.Min(x => x.Key);
            value = values[minDateTime];
            values.Remove(minDateTime);
            return true;
        }
        public bool Contains(T value)
        {
            foreach (DateTime time in values.Keys)
            {
                if (time + ttl > DateTime.UtcNow) values.Remove(time);
            }
            foreach (T value1 in values.Values)
            {
                if (value1.Equals(value)) return true;
            }
            return false;
        }
    }
}
