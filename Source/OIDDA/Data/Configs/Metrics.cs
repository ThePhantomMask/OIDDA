using FlaxEngine;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// OIDDA Metrics
/// </summary>
[Category(name: "OIDDA Data")]
public class Metrics : MetricBase
{
    public bool IndicatesDifficulty(object currentValue, float threshold = OIDDADefaults.DifficultThreshold) => CalculateScore(currentValue) > threshold;
    public bool IndicatesEasy(object currentValue, float threshold = OIDDADefaults.EasyThreshold) => CalculateScore(currentValue) < threshold;

    // Director-oriented wording of the same checks, used by Director rules (stress / relax).
    public bool IndicatesTooStress(object currentValue, float threshold = OIDDADefaults.DifficultThreshold) => CalculateScore(currentValue) > threshold;
    public bool IndicatesRelax(object currentValue, float threshold = OIDDADefaults.EasyThreshold) => CalculateScore(currentValue) < threshold;
}
