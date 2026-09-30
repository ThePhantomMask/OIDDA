using FlaxEngine;
using OIDDA.Data;
using System;

namespace OIDDA;

/// <summary>
/// Director Metrics
/// </summary>
public class DirectorMetrics : MetricBase
{
    public bool IndicatesTooStress(object currentValue, float threshold = 0.7f) => CalculateScore(currentValue) > threshold;

    public bool IndicatesRelax(object currentValue, float threshold = 0.3f) => CalculateScore(currentValue) < threshold;

}
