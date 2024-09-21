using eye.analytics.irmaxtemp.Models;
using System.Collections.Generic;

namespace eye.analytics.irmaxtemp.Calculation
{
    public interface IAnalyticCalculation
    {
        public double Calculate(List<AnalyticInput> input);
    }
}