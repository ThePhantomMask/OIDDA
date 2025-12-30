using FlaxEngine;
using System;
using System.Collections.Generic;
using static OIDDA.ConditionClause;

namespace OIDDA;

/// <summary>
/// OIDDA Gameplay Value struct.
/// </summary>
public struct GameplayValue
{
    public ValueType Type;

    [VisibleIf(nameof(Type), nameof(Type) == "Float")]
    public float FloatValue;
    [VisibleIf(nameof(Type), nameof(Type) == "Int")]
    public int IntValue;
    [VisibleIf(nameof(Type), nameof(Type) == "Bool")]
    public bool BoolValue;
    [VisibleIf(nameof(Type), nameof(Type) == "Vector2")]
    public Vector2 Vector2Value;
    [VisibleIf(nameof(Type), nameof(Type) == "Vector3")]
    public Vector3 Vector3Value;
    [VisibleIf(nameof(Type), nameof(Type) == "Vector4")]
    public Vector4 Vector4Value;
    [VisibleIf(nameof(Type), nameof(Type) == "Color")]
    public Color ColorValue;
    [VisibleIf(nameof(Type), nameof(Type) == "String")]
    public string StringValue;
    [VisibleIf(nameof(Type), nameof(Type) == "Quaternion")]
    public Quaternion QuaternionValue;


    public GameplayValue(float value) : this()
    {
        Type = ValueType.Float;
        FloatValue = value;
    }

    public GameplayValue(int value) : this()
    {
        Type = ValueType.Int;
        IntValue = value;
    }

    public GameplayValue(bool value) : this()
    {
        Type = ValueType.Bool;
        BoolValue = value;
    }

    public GameplayValue(Vector2 value) : this()
    {
        Type = ValueType.Vector2;
        Vector2Value = value;
    }

    public GameplayValue(Vector3 value) : this()
    {
        Type = ValueType.Vector3;
        Vector3Value = value;
    }

    public GameplayValue(Vector4 value) : this()
    {
        Type = ValueType.Vector4;
        Vector4Value = value;
    }

    public GameplayValue(Color value) : this()
    {
        Type = ValueType.Color;
        ColorValue = value;
    }

    public GameplayValue(string value) : this()
    {
        Type = ValueType.String;
        StringValue = value ?? string.Empty;
    }

    public GameplayValue(Quaternion value) : this()
    {
        Type = ValueType.Quaternion;
        QuaternionValue = value;
    }

    public object GetValue()
    {
        return Type switch
        {
            ValueType.Float => FloatValue,
            ValueType.Int => IntValue,
            ValueType.Bool => BoolValue,
            ValueType.Vector2 => Vector2Value,
            ValueType.Vector3 => Vector3Value,
            ValueType.Vector4 => Vector4Value,
            ValueType.Color => ColorValue,
            ValueType.String => StringValue,
            ValueType.Quaternion => QuaternionValue,
            _ => null
        };
    }

    public static GameplayValue FromObject(object value)
    {
        return value switch
        {
            float f => new GameplayValue(f),
            int i => new GameplayValue(i),
            bool b => new GameplayValue(b),
            Vector2 v2 => new GameplayValue(v2),
            Vector3 v3 => new GameplayValue(v3),
            Vector4 v4 => new GameplayValue(v4),
            Color c => new GameplayValue(c),
            string s => new GameplayValue(s),
            Quaternion q => new GameplayValue(q),
            _ => default
        };
    }

    public float AsFloat() => Type == ValueType.Float ? FloatValue : 0f;
    public int AsInt() => Type == ValueType.Int ? IntValue : 0;
    public bool AsBool() => Type == ValueType.Bool && BoolValue;
    public Vector2 AsVector2() => Type == ValueType.Vector2 ? Vector2Value : Vector2.Zero;
    public Vector3 AsVector3() => Type == ValueType.Vector3 ? Vector3Value : Vector3.Zero;
    public Vector4 AsVector4() => Type == ValueType.Vector4 ? Vector4Value : Vector4.Zero;
    public string AsString() => Type == ValueType.String ? StringValue : string.Empty;
    public Color AsColor() => Type == ValueType.Color ? ColorValue : Color.White;
    public Quaternion AsQuaternion() => Type == ValueType.Quaternion ? QuaternionValue : Quaternion.Identity;
}
public enum ValueType
{
    Float,
    Int,
    Bool,
    Vector2,
    Vector3,
    Vector4,
    Color,
    String,
    Quaternion
}

public static class GameplayValueOperations
{
    public static GameplayValue Apply(GameplayValue current, GameplayValue adjustment, AdjustmentOperator op)
    {
        // Type mismatch check
        if (current.Type != adjustment.Type && op != AdjustmentOperator.Set)
        {
            Debug.LogWarning($"Type mismatch: {current.Type} vs {adjustment.Type}");
            return current;
        }

        switch (current.Type)
        {
            case ValueType.Float:
                float resultFloat = op switch
                {
                    AdjustmentOperator.Add => current.FloatValue + adjustment.FloatValue,
                    AdjustmentOperator.Subtract => current.FloatValue - adjustment.FloatValue,
                    AdjustmentOperator.Multiply => current.FloatValue * adjustment.FloatValue,
                    AdjustmentOperator.Divide => adjustment.FloatValue != 0 ? current.FloatValue / adjustment.FloatValue : current.FloatValue,
                    AdjustmentOperator.Set => adjustment.FloatValue,
                    _ => current.FloatValue
                };
                return new GameplayValue(resultFloat);

            case ValueType.Int:
                int resultInt = op switch
                {
                    AdjustmentOperator.Add => current.IntValue + adjustment.IntValue,
                    AdjustmentOperator.Subtract => current.IntValue - adjustment.IntValue,
                    AdjustmentOperator.Multiply => current.IntValue * adjustment.IntValue,
                    AdjustmentOperator.Divide => adjustment.IntValue != 0 ? current.IntValue / adjustment.IntValue : current.IntValue,
                    AdjustmentOperator.Set => adjustment.IntValue,
                    _ => current.IntValue
                };
                return new GameplayValue(resultInt);

            case ValueType.Bool:
                bool resultBool = op switch
                {
                    AdjustmentOperator.Set => adjustment.BoolValue,
                    AdjustmentOperator.Toggle => !current.BoolValue,
                    _ => current.BoolValue
                };
                return new GameplayValue(resultBool);

            case ValueType.Vector3:
                Vector3 resultVec3 = op switch
                {
                    AdjustmentOperator.Add => current.Vector3Value + adjustment.Vector3Value,
                    AdjustmentOperator.Subtract => current.Vector3Value - adjustment.Vector3Value,
                    AdjustmentOperator.Multiply => current.Vector3Value * adjustment.Vector3Value,
                    AdjustmentOperator.Set => adjustment.Vector3Value,
                    _ => current.Vector3Value
                };
                return new GameplayValue(resultVec3);

            case ValueType.String:
                string resultStr = op switch
                {
                    AdjustmentOperator.Set => adjustment.StringValue,
                    AdjustmentOperator.Append => current.StringValue + adjustment.StringValue,
                    _ => current.StringValue
                };
                return new GameplayValue(resultStr);

            // Altri tipi...

            default:
                return current;
        }
    }

    public static GameplayValue Clamp(GameplayValue value, GameplayValue min, GameplayValue max)
    {
        if (value.Type != min.Type || value.Type != max.Type)
        {
            return value;  // Type mismatch, no clamp
        }

        switch (value.Type)
        {
            case ValueType.Float:
                return new GameplayValue(Mathf.Clamp(value.FloatValue, min.FloatValue, max.FloatValue));

            case ValueType.Int:
                return new GameplayValue(Mathf.Clamp(value.IntValue, min.IntValue, max.IntValue));

            case ValueType.Vector3:
                return new GameplayValue(new Vector3(
                    Mathf.Clamp(value.Vector3Value.X, min.Vector3Value.X, max.Vector3Value.X),
                    Mathf.Clamp(value.Vector3Value.Y, min.Vector3Value.Y, max.Vector3Value.Y),
                    Mathf.Clamp(value.Vector3Value.Z, min.Vector3Value.Z, max.Vector3Value.Z)
                ));

            default:
                return value;  // No clamp for other types
        }
    }

    public static bool Compare(GameplayValue a, GameplayValue b, ComparisonOperator op)
    {
        if (a.Type != b.Type) return false;

        switch (a.Type)
        {
            case ValueType.Float:
                return op switch
                {
                    ComparisonOperator.Greater => a.FloatValue > b.FloatValue,
                    ComparisonOperator.Less => a.FloatValue < b.FloatValue,
                    ComparisonOperator.GreaterOrEqual => a.FloatValue >= b.FloatValue,
                    ComparisonOperator.LessOrEqual => a.FloatValue <= b.FloatValue,
                    ComparisonOperator.Equal => Math.Abs(a.FloatValue - b.FloatValue) < 0.001f,
                    ComparisonOperator.NotEqual => Math.Abs(a.FloatValue - b.FloatValue) >= 0.001f,
                    _ => false
                };

            case ValueType.Int:
                return op switch
                {
                    ComparisonOperator.Greater => a.IntValue > b.IntValue,
                    ComparisonOperator.Less => a.IntValue < b.IntValue,
                    ComparisonOperator.GreaterOrEqual => a.IntValue >= b.IntValue,
                    ComparisonOperator.LessOrEqual => a.IntValue <= b.IntValue,
                    ComparisonOperator.Equal => a.IntValue == b.IntValue,
                    ComparisonOperator.NotEqual => a.IntValue != b.IntValue,
                    _ => false
                };

            case ValueType.Bool:
                return op switch
                {
                    ComparisonOperator.Equal => a.BoolValue == b.BoolValue,
                    ComparisonOperator.NotEqual => a.BoolValue != b.BoolValue,
                    _ => false
                };

            case ValueType.String:
                return op switch
                {
                    ComparisonOperator.Equal => a.StringValue == b.StringValue,
                    ComparisonOperator.NotEqual => a.StringValue != b.StringValue,
                    ComparisonOperator.Contains => a.StringValue.Contains(b.StringValue),
                    _ => false
                };

            default:
                return false;
        }
    }
}