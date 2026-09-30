using FlaxEngine;
using OIDDA.Data;
using System;

namespace OIDDA;

/// <summary>
/// DDA Metrics
/// </summary>
[Category(name: "OIDDA Data")]
public class DDAMetrics : MetricBase
{
    public bool IndicatesDifficulty(object currentValue, float threshold = 0.7f) => CalculateScore(currentValue) > threshold;

    public bool IndicatesEasy(object currentValue, float threshold = 0.3f) => CalculateScore(currentValue) < threshold;
}