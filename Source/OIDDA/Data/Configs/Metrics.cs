using System;
using System.Collections.Generic;
using FlaxEngine;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// OIDDA Metrics
/// </summary>
[Category(name: "OIDDA Data")]
public class Metrics : MetricBase
{
    // For DDA side
    public bool IndicatesDifficulty(object currentValue, float threshold = 0.7f) => CalculateScore(currentValue) > threshold;
    public bool IndicatesEasy(object currentValue, float threshold = 0.3f) => CalculateScore(currentValue) < threshold;

    // For Director side
    public bool IndicatesTooStress(object currentValue, float threshold = 0.7f) => CalculateScore(currentValue) > threshold;
    public bool IndicatesRelax(object currentValue, float threshold = 0.3f) => CalculateScore(currentValue) < threshold;
}
