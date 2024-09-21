using eye.analytics.irmaxtemp.Models;
using System.Collections.Generic;


namespace eye.analytics.irmaxtemp.Calculation
{
    public class AnalyticCalculation : IAnalyticCalculation
    {
        public double Calculate(List<AnalyticInput> input)
        {
            double res = 0;
            foreach (AnalyticInput item in input) { 
               res = double.Max(res, item.latest_value);
            }
            return res; 
        }

    }
}