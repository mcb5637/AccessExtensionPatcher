using System;

namespace AccessExtension
{
    internal struct AEPData<T> where T : class
    {
        private T d;
        private readonly Func<T> get;
        public T Data
        {
            get
            {
                if (d == null)
                    d = get();
                return d;
            }
        }
        internal AEPData(T d)
        {
            if (d == null)
                throw new ArgumentNullException("d");
            this.d = d;
            get = null;
        }
        internal AEPData(Func<T> get)
        {
            if (get == null)
                throw new ArgumentNullException("get");
            this.get = get;
            d = null;
        }
    }
}
