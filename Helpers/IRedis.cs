using System;

namespace eye.analytics.irmaxtemp.Helpers
{
    public interface IRedis : IDisposable
    {
        void Set(string key, string value);
        void Set(string key, byte[] value);
        string[] LeftPop(string key, int limit);
        byte[] LeftPop(string key);
        void RightPush(string key, string value);
        void RightPush(string key, byte[] value);
    }
}