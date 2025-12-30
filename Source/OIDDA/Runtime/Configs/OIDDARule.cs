using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Rule
/// </summary>
public class OIDDARule
{
    public string TargetGlobalVariable;
    public float AdjustmentAmount;
    public float MinValue = float.MinValue;
    public float MaxValue = float.MaxValue;
    public OIDDACondition Condition;
    public List<OIDDARuleException> Exceptions;

    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (!Condition.IsMet(metrics)) return;

        if (HasActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        ApplyToGlobalsVariables();
    }

    protected bool HasActiveException(Dictionary<string, object> metrics, out OIDDARuleException activeException)
    {
        activeException = null;
        if (Exceptions == null || Exceptions.Count is 0) return false;

        foreach (var exception in Exceptions)
        {
            if (exception.Condition.IsMet(metrics))
            {
                activeException = exception;
                return true;
            }
        }
        return false;
    }

    protected virtual void ApplyToGlobalsVariables()
    {
        ORS.Instance.ConnectORSAgent(ORSUtils.ORSType.ReceiverSender);
        var currentValue = ORS.Instance.ReceiverValue<float>(TargetGlobalVariable);
        var newValue = currentValue + AdjustmentAmount;
        newValue = Mathf.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.SenderValue(TargetGlobalVariable, newValue);
        ORS.Instance.DisconnectORSAgent(ORSUtils.ORSType.ReceiverSender);
    }
}
