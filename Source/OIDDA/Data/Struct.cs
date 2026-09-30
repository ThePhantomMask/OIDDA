using FlaxEngine;
using System;
using System.Collections.Generic;

namespace OIDDA.Data;

public struct MetricInfo
{
    public string MetricName;
    public object CurrentValue;
    public float NormalizedScore;    // 0-1
    public float WeightedScore;      // Score * Weight
    public float Weight;
    public bool IsOutOfBounds;
    public MetricState State;
}