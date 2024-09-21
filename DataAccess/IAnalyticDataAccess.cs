using System.Threading.Tasks;
using System.Collections.Generic;
using eye.analytics.irmaxtemp.Models;

namespace eye.analytics.irmaxtemp.DataAccess
{
    public interface IAnalyticDataAccess
    {
        Task<List<ConfiguredIrAnalytic>> GetAnalyticsConfigured();
    }
}
