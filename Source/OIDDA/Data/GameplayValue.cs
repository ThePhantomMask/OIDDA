using FlaxEngine;
using OIDDA.Data;
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

    bool IsFloat => Type is ValueType.Float;
    bool IsInt => Type is ValueType.Int;
    bool IsBool => Type is ValueType.Bool;
    bool IsVector2 => Type is ValueType.Vector2;
    bool IsVector3 => Type is ValueType.Vector3;
    bool IsVector4 => Type is ValueType.Vector4;
    bool IsColor => Type is ValueType.Color;
    bool IsString => Type is ValueType.String;
    bool IsQuaternion => Type is ValueType.Quaternion;
    bool IsTransform => Type is ValueType.Transform;
    bool IsBoundingBox => Type is ValueType.BoundingBox;
    bool IsBoundingSphere => Type is ValueType.BoundingSphere;
    bool IsRectangle => Type is ValueType.Rectangle;
    bool IsMatrix => Type is ValueType.Matrix;
    bool IsTexture => Type is ValueType.Texture;
    bool IsCubeTexture => Type is ValueType.CubeTexture;

    [VisibleIf(nameof(IsFloat))]
    public float FloatValue;
    [VisibleIf(nameof(IsInt))]
    public int IntValue;
    [VisibleIf(nameof(IsBool))]
    public bool BoolValue;
    [VisibleIf(nameof(IsVector2))]
    public Vector2 Vector2Value;
    [VisibleIf(nameof(IsVector3))]
    public Vector3 Vector3Value;
    [VisibleIf(nameof(IsVector4))]
    public Vector4 Vector4Value;
    [VisibleIf(nameof(IsColor))]
    public Color ColorValue;
    [VisibleIf(nameof(IsString))]
    public string StringValue;
    [VisibleIf(nameof(IsQuaternion))]
    public Quaternion QuaternionValue;
    [VisibleIf(nameof(IsTransform))]
    public Transform TransformValue;
    [VisibleIf(nameof(IsBoundingBox))]
    public BoundingBox BoundingBoxValue;
    [VisibleIf(nameof(IsBoundingSphere))]
    public BoundingSphere BoundingSphereValue;
    [VisibleIf(nameof(IsRectangle))]
    public Rectangle RectangleValue;
    [VisibleIf(nameof(IsMatrix))]
    public Matrix MatrixValue;
    [VisibleIf(nameof(IsTexture))]
    public Texture TextureValue;
    [VisibleIf(nameof(IsCubeTexture))]
    public CubeTexture CubeTextureValue;

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

    public GameplayValue(Transform value) : this()
    {
        Type = ValueType.Transform;
        TransformValue = value;
    }

    public GameplayValue(BoundingBox value) : this()
    {
        Type = ValueType.BoundingBox;
        BoundingBoxValue = value;
    }

    public GameplayValue(BoundingSphere value) : this()
    {
        Type = ValueType.BoundingSphere;
        BoundingSphereValue = value;
    }

    public GameplayValue(Rectangle value) : this()
    {
        Type = ValueType.Rectangle;
        RectangleValue = value;
    }

    public GameplayValue(Matrix value) : this()
    {
        Type = ValueType.Matrix;
        MatrixValue = value;
    }

    public GameplayValue(Texture value): this()
    {
        Type = ValueType.Texture;
        TextureValue = value;
    }

    public GameplayValue(CubeTexture value) : this()
    {
        Type = ValueType.CubeTexture;
        CubeTextureValue = value;
    }

    public object Value => Type switch
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
        ValueType.Transform => TransformValue,
        ValueType.BoundingBox => BoundingBoxValue,
        ValueType.BoundingSphere => BoundingSphereValue,
        ValueType.Rectangle => RectangleValue,
        ValueType.Matrix => MatrixValue,
        ValueType.Texture => TextureValue,
        ValueType.CubeTexture => CubeTextureValue,
        _ => null
    };

    public static GameplayValue ConvertObject(object value)
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
            Transform t => new GameplayValue(t),
            BoundingBox bb => new GameplayValue(bb),
            BoundingSphere bs => new GameplayValue(bs),
            Rectangle r => new GameplayValue(r),
            Matrix m => new GameplayValue(m),
            Texture t => new GameplayValue(t),
            CubeTexture ct => new GameplayValue(ct),
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
    public Transform AsTransform() => Type == ValueType.Transform ? TransformValue : Transform.Default;
    public BoundingBox AsBoundingBox() => Type == ValueType.BoundingBox ? BoundingBoxValue : BoundingBox.Default;
    public BoundingSphere AsBoundingSphere() => Type == ValueType.BoundingSphere ? BoundingSphereValue : BoundingSphere.Default;
    public Texture AsTexture() => Type == ValueType.Texture ? TextureValue : new Texture();
    public CubeTexture AsCubeTexture() => Type == ValueType.CubeTexture ? CubeTextureValue : new CubeTexture();

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
    Quaternion,
    Transform,
    BoundingBox,
    BoundingSphere,
    Rectangle,
    Matrix,
    Texture,
    CubeTexture
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

        return current.Type switch
        {
            ValueType.Float => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.FloatValue + adjustment.FloatValue,
                AdjustmentOperator.Subtract => current.FloatValue - adjustment.FloatValue,
                AdjustmentOperator.Multiply => current.FloatValue * adjustment.FloatValue,
                AdjustmentOperator.Divide => adjustment.FloatValue != 0 ? current.FloatValue / adjustment.FloatValue : current.FloatValue,
                AdjustmentOperator.Set => adjustment.FloatValue,
                _ => current.FloatValue
            }),
            ValueType.Int => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.IntValue + adjustment.IntValue,
                AdjustmentOperator.Subtract => current.IntValue - adjustment.IntValue,
                AdjustmentOperator.Multiply => current.IntValue * adjustment.IntValue,
                AdjustmentOperator.Divide => adjustment.IntValue != 0 ? current.IntValue / adjustment.IntValue : current.IntValue,
                AdjustmentOperator.Set => adjustment.IntValue,
                _ => current.IntValue
            }),
            ValueType.Bool => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.BoolValue,
                AdjustmentOperator.Toggle => !current.BoolValue,
                _ => current.BoolValue
            }),
            ValueType.Vector2 => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.Vector2Value + adjustment.Vector2Value,
                AdjustmentOperator.Subtract => current.Vector2Value - adjustment.Vector2Value,
                AdjustmentOperator.Multiply => current.Vector2Value * adjustment.Vector2Value,
                AdjustmentOperator.Divide => adjustment.Vector2Value != Vector2.Zero ? current.Vector2Value / adjustment.Vector2Value : current.Vector2Value,
                AdjustmentOperator.Set => adjustment.Vector2Value,
                _ => current.Vector2Value
            }),
            ValueType.Vector3 => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.Vector3Value + adjustment.Vector3Value,
                AdjustmentOperator.Subtract => current.Vector3Value - adjustment.Vector3Value,
                AdjustmentOperator.Multiply => current.Vector3Value * adjustment.Vector3Value,
                AdjustmentOperator.Divide => adjustment.Vector3Value != Vector3.Zero ? current.Vector3Value / adjustment.Vector3Value : current.Vector3Value,
                AdjustmentOperator.Set => adjustment.Vector3Value,
                _ => current.Vector3Value
            }),
            ValueType.Vector4 => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.Vector4Value + adjustment.Vector4Value,
                AdjustmentOperator.Subtract => current.Vector4Value - adjustment.Vector4Value,
                AdjustmentOperator.Multiply => current.Vector4Value * adjustment.Vector4Value,
                AdjustmentOperator.Divide => adjustment.Vector4Value != Vector4.Zero ? current.Vector4Value / adjustment.Vector4Value : current.Vector4Value,
                AdjustmentOperator.Set => adjustment.Vector4Value,
                _ => current.Vector4Value
            }),
            ValueType.String => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.StringValue,
                AdjustmentOperator.Append => current.StringValue + adjustment.StringValue,
                _ => current.StringValue
            }),
            ValueType.Quaternion => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.QuaternionValue + adjustment.QuaternionValue,
                AdjustmentOperator.Subtract => current.QuaternionValue - adjustment.QuaternionValue,
                AdjustmentOperator.Multiply => current.QuaternionValue * adjustment.QuaternionValue,
                AdjustmentOperator.Set => adjustment.QuaternionValue,
                _ => current.QuaternionValue
            }),
            ValueType.Transform => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.TransformValue + adjustment.TransformValue,
                AdjustmentOperator.Subtract => current.TransformValue - adjustment.TransformValue,
                AdjustmentOperator.Set => adjustment.TransformValue,
                _ => current.TransformValue
            }),
            ValueType.BoundingBox => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.BoundingBoxValue,
                _ => current.BoundingBoxValue
            }),
            ValueType.BoundingSphere => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.BoundingSphereValue,
                _ => current.BoundingSphereValue
            }),
            ValueType.Rectangle => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.RectangleValue,
                _ => current.RectangleValue
            }),
            ValueType.Matrix => new GameplayValue(op switch
            {
                AdjustmentOperator.Add => current.MatrixValue + adjustment.MatrixValue,
                AdjustmentOperator.Subtract => current.MatrixValue - adjustment.MatrixValue,
                AdjustmentOperator.Multiply => current.MatrixValue * adjustment.MatrixValue,
                AdjustmentOperator.Divide => adjustment.MatrixValue != Matrix.Zero ? current.MatrixValue / adjustment.MatrixValue : current.MatrixValue,
                AdjustmentOperator.Set => adjustment.MatrixValue,
                _ => current.MatrixValue
            }),
            ValueType.Texture => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.TextureValue,
                _ => current.TextureValue
            }),
            ValueType.CubeTexture => new GameplayValue(op switch
            {
                AdjustmentOperator.Set => adjustment.CubeTextureValue,
                _ => current.CubeTextureValue
            }),
            _ => current
        };
    }

    public static GameplayValue Clamp(GameplayValue value, GameplayValue min, GameplayValue max)
    {
        // Type mismatch, no clamp
        if (value.Type != min.Type || value.Type != max.Type)
            return value;

        return value.Type switch
        {
            ValueType.Float => new GameplayValue(Mathf.Clamp(value.FloatValue, min.FloatValue, max.FloatValue)),
            ValueType.Int => new GameplayValue(Mathf.Clamp(value.IntValue, min.IntValue, max.IntValue)),
            ValueType.Vector2 => new GameplayValue(new Vector2(
                    Mathf.Clamp(value.Vector2Value.X, min.Vector2Value.X, max.Vector2Value.X),
                    Mathf.Clamp(value.Vector2Value.Y, min.Vector2Value.Y, max.Vector2Value.Y))),
            ValueType.Vector3 => new GameplayValue(new Vector3(
                    Mathf.Clamp(value.Vector3Value.X, min.Vector3Value.X, max.Vector3Value.X),
                    Mathf.Clamp(value.Vector3Value.Y, min.Vector3Value.Y, max.Vector3Value.Y),
                    Mathf.Clamp(value.Vector3Value.Z, min.Vector3Value.Z, max.Vector3Value.Z))),
            ValueType.Vector4 => new GameplayValue(new Vector4(
                    Mathf.Clamp(value.Vector4Value.X, min.Vector4Value.X, max.Vector4Value.X),
                    Mathf.Clamp(value.Vector4Value.Y, min.Vector4Value.Y, max.Vector4Value.Y),
                    Mathf.Clamp(value.Vector4Value.Z, min.Vector4Value.Z, max.Vector4Value.Z),
                    Mathf.Clamp(value.Vector4Value.W, min.Vector4Value.W, max.Vector4Value.W))),
            ValueType.Quaternion => new GameplayValue(new Quaternion(
                    Mathf.Clamp(value.QuaternionValue.X, min.QuaternionValue.X, max.QuaternionValue.X),
                    Mathf.Clamp(value.QuaternionValue.Y, min.QuaternionValue.Y, max.QuaternionValue.Y),
                    Mathf.Clamp(value.QuaternionValue.Z, min.QuaternionValue.Z, max.QuaternionValue.Z),
                    Mathf.Clamp(value.QuaternionValue.W, min.QuaternionValue.W, max.QuaternionValue.W))),
            ValueType.Matrix => new GameplayValue(new Matrix(
                     Mathf.Clamp(value.MatrixValue.M11, min.MatrixValue.M11, max.MatrixValue.M11),
                     Mathf.Clamp(value.MatrixValue.M12, min.MatrixValue.M12, max.MatrixValue.M12),
                     Mathf.Clamp(value.MatrixValue.M13, min.MatrixValue.M13, max.MatrixValue.M13),
                     Mathf.Clamp(value.MatrixValue.M14, min.MatrixValue.M14, max.MatrixValue.M14),
                     Mathf.Clamp(value.MatrixValue.M21, min.MatrixValue.M21, max.MatrixValue.M21),
                     Mathf.Clamp(value.MatrixValue.M22, min.MatrixValue.M22, max.MatrixValue.M22),
                     Mathf.Clamp(value.MatrixValue.M23, min.MatrixValue.M23, max.MatrixValue.M23),
                     Mathf.Clamp(value.MatrixValue.M24, min.MatrixValue.M24, max.MatrixValue.M24),
                     Mathf.Clamp(value.MatrixValue.M31, min.MatrixValue.M31, max.MatrixValue.M31),
                     Mathf.Clamp(value.MatrixValue.M32, min.MatrixValue.M32, max.MatrixValue.M32),
                     Mathf.Clamp(value.MatrixValue.M33, min.MatrixValue.M33, max.MatrixValue.M33),
                     Mathf.Clamp(value.MatrixValue.M34, min.MatrixValue.M34, max.MatrixValue.M34),
                     Mathf.Clamp(value.MatrixValue.M41, min.MatrixValue.M41, max.MatrixValue.M41),
                     Mathf.Clamp(value.MatrixValue.M42, min.MatrixValue.M42, max.MatrixValue.M42),
                     Mathf.Clamp(value.MatrixValue.M43, min.MatrixValue.M43, max.MatrixValue.M43),
                     Mathf.Clamp(value.MatrixValue.M44, min.MatrixValue.M44, max.MatrixValue.M44))),
            ValueType.Color => new GameplayValue(new Color(
                    Mathf.Clamp(value.ColorValue.R, min.ColorValue.R, max.ColorValue.R),
                    Mathf.Clamp(value.ColorValue.G, min.ColorValue.G, max.ColorValue.G),
                    Mathf.Clamp(value.ColorValue.B, min.ColorValue.B, max.ColorValue.B),
                    Mathf.Clamp(value.ColorValue.A, min.ColorValue.A, max.ColorValue.A))),
            _ => value // No clamp for other types
        };
    }

    public static bool Compare(GameplayValue a, GameplayValue b, ComparisonOperator op)
    {
        if (a.Type != b.Type) 
            return false;

        return a.Type switch
        {
            ValueType.Float => op switch
            {
                ComparisonOperator.Greater => a.FloatValue > b.FloatValue,
                ComparisonOperator.Less => a.FloatValue < b.FloatValue,
                ComparisonOperator.GreaterOrEqual => a.FloatValue >= b.FloatValue,
                ComparisonOperator.LessOrEqual => a.FloatValue <= b.FloatValue,
                ComparisonOperator.Equal => Math.Abs(a.FloatValue - b.FloatValue) < 0.001f,
                ComparisonOperator.NotEqual => Math.Abs(a.FloatValue - b.FloatValue) >= 0.001f,
                _ => false
            },
            ValueType.Int => op switch
            {
                ComparisonOperator.Greater => a.IntValue > b.IntValue,
                ComparisonOperator.Less => a.IntValue < b.IntValue,
                ComparisonOperator.GreaterOrEqual => a.IntValue >= b.IntValue,
                ComparisonOperator.LessOrEqual => a.IntValue <= b.IntValue,
                ComparisonOperator.Equal => a.IntValue == b.IntValue,
                ComparisonOperator.NotEqual => a.IntValue != b.IntValue,
                _ => false
            },
            ValueType.Bool => op switch
            {
                ComparisonOperator.Equal => a.BoolValue == b.BoolValue,
                ComparisonOperator.NotEqual => a.BoolValue != b.BoolValue,
                _ => false
            },
            ValueType.String => op switch
            {
                ComparisonOperator.Equal => a.StringValue == b.StringValue,
                ComparisonOperator.NotEqual => a.StringValue != b.StringValue,
                ComparisonOperator.Contains => a.StringValue.Contains(b.StringValue),
                _ => false
            },
            ValueType.Vector2 => op switch
            {
                ComparisonOperator.Greater => a.Vector2Value.ValuesSum > b.Vector2Value.ValuesSum,
                ComparisonOperator.Less => a.Vector2Value.ValuesSum < b.Vector2Value.ValuesSum,
                ComparisonOperator.GreaterOrEqual => a.Vector2Value.ValuesSum >= b.Vector2Value.ValuesSum,
                ComparisonOperator.LessOrEqual => a.Vector2Value.ValuesSum <= b.Vector2Value.ValuesSum,
                ComparisonOperator.Equal => a.Vector2Value.ValuesSum == b.Vector2Value.ValuesSum,
                ComparisonOperator.NotEqual => a.Vector2Value.ValuesSum != b.Vector2Value.ValuesSum,
                _ => false
            },
            ValueType.Vector3 => op switch
            {
                ComparisonOperator.Greater => a.Vector3Value.ValuesSum > b.Vector3Value.ValuesSum,
                ComparisonOperator.Less => a.Vector3Value.ValuesSum < b.Vector3Value.ValuesSum,
                ComparisonOperator.GreaterOrEqual => a.Vector3Value.ValuesSum >= b.Vector3Value.ValuesSum,
                ComparisonOperator.LessOrEqual => a.Vector3Value.ValuesSum <= b.Vector3Value.ValuesSum,
                ComparisonOperator.Equal => a.Vector3Value.ValuesSum == b.Vector3Value.ValuesSum,
                ComparisonOperator.NotEqual => a.Vector3Value.ValuesSum != b.Vector3Value.ValuesSum,
                _ => false
            },
            ValueType.Vector4 => op switch
            {
                ComparisonOperator.Greater => a.Vector4Value.ValuesSum > b.Vector4Value.ValuesSum,
                ComparisonOperator.Less => a.Vector4Value.ValuesSum < b.Vector4Value.ValuesSum,
                ComparisonOperator.GreaterOrEqual => a.Vector4Value.ValuesSum >= b.Vector4Value.ValuesSum,
                ComparisonOperator.LessOrEqual => a.Vector4Value.ValuesSum <= b.Vector4Value.ValuesSum,
                ComparisonOperator.Equal => a.Vector4Value.ValuesSum == b.Vector4Value.ValuesSum,
                ComparisonOperator.NotEqual => a.Vector4Value.ValuesSum != b.Vector4Value.ValuesSum,
                _ => false
            },
            ValueType.Color => op switch
            {
                ComparisonOperator.Greater => a.ColorValue.ValuesSum > b.ColorValue.ValuesSum,
                ComparisonOperator.Less => a.ColorValue.ValuesSum < b.ColorValue.ValuesSum,
                ComparisonOperator.GreaterOrEqual => a.ColorValue.ValuesSum >= b.ColorValue.ValuesSum,
                ComparisonOperator.LessOrEqual => a.ColorValue.ValuesSum <= b.ColorValue.ValuesSum,
                ComparisonOperator.Equal => a.ColorValue.ValuesSum == b.ColorValue.ValuesSum,
                ComparisonOperator.NotEqual => a.ColorValue.ValuesSum != b.ColorValue.ValuesSum,
                _ => false
            },
            ValueType.Quaternion => op switch
            {
                ComparisonOperator.Greater => a.QuaternionValue.Angle > b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum > b.QuaternionValue.Axis.ValuesSum,
                ComparisonOperator.Less => a.QuaternionValue.Angle < b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum < b.QuaternionValue.Axis.ValuesSum,
                ComparisonOperator.GreaterOrEqual => a.QuaternionValue.Angle >= b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum >= b.QuaternionValue.Axis.ValuesSum,
                ComparisonOperator.LessOrEqual => a.QuaternionValue.Angle <= b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum <= b.QuaternionValue.Axis.ValuesSum,
                ComparisonOperator.Equal => a.QuaternionValue.Angle == b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum == b.QuaternionValue.Axis.ValuesSum,
                ComparisonOperator.NotEqual => a.QuaternionValue.Angle != b.QuaternionValue.Angle &&
                a.QuaternionValue.Axis.ValuesSum != b.QuaternionValue.Axis.ValuesSum,
                _ => false
            },
            _ => false
        };
    }

    public static GameplayValue Lerp(GameplayValue current, GameplayValue target, float t)
    {
        t = Mathf.Saturate(t);

        if (current.Type != target.Type) 
            return target;

        return current.Type switch
        {
            ValueType.Float => new GameplayValue(Mathf.Lerp(current.FloatValue, target.FloatValue, t)),
            ValueType.Int => new GameplayValue((int)Math.Round((float)Mathf.Lerp(current.IntValue, target.IntValue, t))),
            ValueType.Vector2 => new GameplayValue(Vector2.Lerp(current.Vector2Value, target.Vector2Value, t)),
            ValueType.Vector3 => new GameplayValue(Vector3.Lerp(current.Vector3Value, target.Vector3Value, t)),
            ValueType.Vector4 => new GameplayValue(Vector4.Lerp(current.Vector4Value, target.Vector4Value, t)),
            ValueType.Color => new GameplayValue(Color.Lerp(current.ColorValue, target.ColorValue, t)),
            ValueType.Quaternion => new GameplayValue(Quaternion.Lerp(current.QuaternionValue, target.QuaternionValue, t)),
            ValueType.Matrix => new GameplayValue(Matrix.Lerp(current.MatrixValue, target.MatrixValue, t)),
            ValueType.Transform => new GameplayValue(Transform.Lerp(current.TransformValue, target.TransformValue, t)),
            ValueType.Bool => t > 0.5f ? target : current,
            _ or ValueType.String => target
        };
    }

    public static bool IsNearTarget(GameplayValue current, GameplayValue target, float threshold = 0.01f)
    {
        if (current.Type != target.Type) 
            return false;

        return current.Type switch
        {
            ValueType.Float => Math.Abs(current.FloatValue - target.FloatValue) < threshold,
            ValueType.Vector2 => Vector2.Distance(current.Vector2Value, target.Vector2Value) < threshold,
            ValueType.Vector3 => Vector3.Distance(current.Vector3Value, target.Vector3Value) < threshold,
            ValueType.Vector4 => Vector4.Distance(current.Vector4Value, target.Vector4Value) < threshold,
            ValueType.Quaternion => Quaternion.Dot(current.QuaternionValue, target.QuaternionValue) > 1.0f - threshold,
            ValueType.Int => current.IntValue == target.IntValue,
            ValueType.Color => current.ColorValue == target.ColorValue,
            ValueType.Bool => current.BoolValue == target.BoolValue,
            ValueType.String => current.StringValue == target.StringValue,
            ValueType.Texture => current.TextureValue == target.TextureValue,
            ValueType.CubeTexture => current.CubeTextureValue == target.CubeTextureValue,
            _ => true
        };
    }
}