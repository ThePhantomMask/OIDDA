using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Metrics
/// </summary>
public class OIDDAMetrics
{
    public string MetricName;
    [Range(0, 1)] public float Weight;
    public float ThresholdMin;
    public float ThresholdMax;
    public bool InverseLogic;



}
