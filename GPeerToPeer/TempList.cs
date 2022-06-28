namespace GPeerToPeer
{
    public class TempList<T>
    {
        private readonly Dictionary<DateTime, T> values;
        private readonly object valuesLock;
        private TimeSpan Ttl { get; set; }
        public TempList(long milliseconds)
        {
            values = new Dictionary<DateTime, T>();
            valuesLock = new object();
            Ttl = TimeSpan.FromMilliseconds(milliseconds);
        }
        private void DeleteOldValues()
        {
            foreach (DateTime time in values.Keys)
            {
                if (time + Ttl < DateTime.UtcNow) values.Remove(time);
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
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task<T> t = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
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
                return default;
            }, ct);
            bool isManaged = t.Wait(Ttl);
            tokenSource.Cancel();
            if (isManaged)
            {
                value = t.GetAwaiter().GetResult();
                return true;
            }
            return false;
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
