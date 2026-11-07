using System.Collections.Generic;
using System;

namespace OIDDA.Network;

/// <summary>
/// The outcome of checking a metric report.
/// </summary>
public enum OIDDAMetricVerdict
{
    /// <summary> The report is taken (with its value clamped to the metric's range). </summary>
    Accepted,

    /// <summary> The client sent more reports than it may per second: this one is dropped. Nothing is wrong with the report itself. </summary>
    RateLimited,

    /// <summary> The name is empty, too long or not on the allow-list, or the value is not a finite number. </summary>
    Invalid
}

/// <summary>
/// Decides which metric reports from clients the server accepts. A client is not trusted: only metrics on the allow-list are
/// taken, values are clamped to their range, and each client has a limited number of reports per second.
/// </summary>
public sealed class OIDDAMetricGate
{
    public const int MaxNameLength = 64;

    sealed class Bucket
    {
        public float Tokens;
        public float LastTime;
    }

    readonly Dictionary<string, (float Min, float Max)> _allowed = new();
    readonly Dictionary<uint, Bucket> _buckets = new();
    readonly float _perSecond;

    public int Accepted { get; private set; }
    public int Rejected { get; private set; }

    /// <param name="allowed">Metrics clients may report. Empty means none.</param>
    /// <param name="maxReportsPerSecond">Reports per second and per client. 0 or less rejects everything.</param>
    public OIDDAMetricGate(IEnumerable<AllowedClientMetric> allowed, int maxReportsPerSecond)
    {
        _perSecond = maxReportsPerSecond;

        if (allowed == null) return;

        foreach (var metric in allowed)
        {
            if (metric == null || string.IsNullOrEmpty(metric.Name) || metric.Name.Length > MaxNameLength) continue;
            _allowed[metric.Name] = metric.Min <= metric.Max ? (metric.Min, metric.Max) : (metric.Max, metric.Min);
        }
    }

    /// <summary>
    /// Decides whether to accept or reject a value received from a sender
    /// </summary>
    /// <param name="senderId">Sender ID</param>
    /// <param name="name">Data's name</param>
    /// <param name="value">Data's value</param>
    /// <param name="now">Current time in seconds, from a clock that always moves forward.</param>
    /// <param name="clamped">The value to use, inside the metric's range.</param>
    public bool TryAccept(uint senderId, string name, float value, float now, out float clamped)
        => Check(senderId, name, value, now, out clamped) == OIDDAMetricVerdict.Accepted;

    /// <summary>
    /// Same decision as <see cref="TryAccept"/>, but tells why a report was refused: because the client sent too many, or because the
    /// report itself is not valid.
    /// </summary>
    public OIDDAMetricVerdict Check(uint senderId, string name, float value, float now, out float clamped)
    {
        clamped = 0f;

        // Every report costs a token, valid or not, so a flood of garbage cannot get through either.
        if (!TakeToken(senderId, now)) return Reject(OIDDAMetricVerdict.RateLimited);

        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) return Reject(OIDDAMetricVerdict.Invalid);
        if (!_allowed.TryGetValue(name, out var range)) return Reject(OIDDAMetricVerdict.Invalid);
        if (!float.IsFinite(value)) return Reject(OIDDAMetricVerdict.Invalid);

        clamped = Math.Clamp(value, range.Min, range.Max);
        Accepted++;
        return OIDDAMetricVerdict.Accepted;
    }

    /// <summary> 
    /// Forgets a client's rate-limit state (when they leave). 
    /// </summary>
    public void Forget(uint senderId) => _buckets.Remove(senderId);

    OIDDAMetricVerdict Reject(OIDDAMetricVerdict verdict)
    {
        Rejected++;
        return verdict;
    }

    private bool TakeToken(uint senderId, float now)
    {
        if (_perSecond <= 0f) return false;

        if (!_buckets.TryGetValue(senderId, out var bucket))
        {
            bucket = new Bucket { Tokens = _perSecond, LastTime = now };
            _buckets[senderId] = bucket;
        }

        float elapsed = now - bucket.LastTime;
        if (elapsed > 0f) bucket.Tokens = Math.Min(_perSecond, bucket.Tokens + elapsed * _perSecond);
        bucket.LastTime = now;

        if (bucket.Tokens < 1f) return false;

        bucket.Tokens -= 1f;
        return true;
    }
}
